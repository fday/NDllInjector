
include 'win32w.inc'

format binary
use32

; params for threadproc
struct PROCPARAMS
    LoadLibraryA     dd ?
    GetProcAddress   dd ?
    runtime	     dd ?    ; unicode
    dllPath	     dd ?    ; unicode
    className	     dd ?    ; unicode
    functionName     dd ?    ; unicode
ends


include 'typedef.inc'


threadproc:
	call	$+5						; calc base address
	pop	eax
	sub	eax, 5						; now, eax contains address of threadproc

	push	ebp
	mov	ebp, esp					; create stack frame
label .param dword at ebp+8					; param argument is located at ebp+8 and point to PROCPARAMS struct

	push	eax						; save .base address
	sub	esp, 4
label .base	    dword at ebp-4				; .base is effective address of threadproc
label .runtimeHost  dword at ebp-8				; .runtimeHost is address of ICLRRuntimeHost

	add	eax, mscoree - threadproc			; now eax is real address of mscoree
	push	eax

	mov	eax, dword [.param]				; eax point to PROCPARAMS struct
	call	dword [eax + PROCPARAMS.LoadLibraryA]		; call LoadLibraryA

	test	eax, eax					; check for errors
	jnz	@f
	inc	eax						; early exit
	jmp	.exit
@@:
	mov	edx, [.base]
	add	edx, CorBindToRuntimeEx - threadproc	       ; now edx is real address of "CorBindToRuntimeEx" string

	push	edx						; push function name
	push	eax						; push dll handle

	mov	eax, dword [.param]
	call	dword [eax + PROCPARAMS.GetProcAddress]

	test	eax, eax					; check for errors
	jnz	@f
	inc	eax
	jmp	.exit
@@:
	; HRESULT hr = ::CorBindToRuntimeEx(runtime, L"wks", 0, CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, &runtimeHost);

	mov	edx, eax					; edx = address of CorBindToRuntimeEx

	lea	eax, dword [.runtimeHost]			     ; address of .runtimeHost
	push	eax

	mov	eax, [.base]

	lea	ecx, [eax + IID_ICLRRuntimeHost - threadproc]	; now ecx is real address of IID_ICLRRuntimeHost
	push	ecx

	lea	ecx, [eax + CLSID_CLRRuntimeHost - threadproc]	; now ecx is real address of CLSID_CLRRuntimeHost
	push	ecx

	push	dword 0

	lea	ecx, [eax + wks - threadproc]			; now ecx is real address of wks
	push	ecx

	mov	eax, dword [.param]
	push	dword [eax + PROCPARAMS.runtime]		; push address of runtime version string

	call	edx						; call CorBindToRuntimeEx
	failed	eax, .exit

	; runtimeHost->Start()
	comcall dword [.runtimeHost], ICLRRuntimeHost, Start	; call start
	failed	eax, .exit

	; hr = runtimeHost->ExecuteInDefaultAppDomain(dllPath, className, functionName, param, &dwRet);
	push	0
	push	0

	mov	eax, [.param]
	push	dword [eax + PROCPARAMS.functionName]
	push	dword [eax + PROCPARAMS.className]
	push	dword [eax + PROCPARAMS.dllPath]

	comcall dword [.runtimeHost], ICLRRuntimeHost, ExecuteInDefaultAppDomain	 ; call ExecuteInDefaultAppDomain
	failed	eax, .exit

	comcall dword [.runtimeHost], ICLRRuntimeHost, Release
	failed	eax, .exit

	xor	eax, eax					; success

.exit:

	leave
	retn	4

include 'datadef.inc'
