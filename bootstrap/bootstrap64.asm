
include 'win64w.inc'

; format binary
use64

; params for threadproc
struct PROCPARAMS
    LoadLibraryA     dq ?
    GetProcAddress   dq ?
    runtime	     dq ?    ; unicode
    dllPath	     dq ?    ; unicode
    className	     dq ?    ; unicode
    functionName     dq ?    ; unicode
ends

include 'typedef.inc'

function_args_size  equ  (6*8)
vars_count	    equ  2
stack_frame	    equ  (function_args_size+vars_count*8)

entry_point:

	mov	qword [rsp+8], rcx				; save argument PROCPARAMS struct onto stack in place reserved by caller
	push	rdi
	sub	rsp, stack_frame				; max of args for func call +2 var
	mov	rdi, rsp					; create stack frame


	lea	rdx, [rip]
threadproc:

label .param	    qword at rsp+stack_frame+2*8		; param argument is located at rbp+8 and point to PROCPARAMS struct
label .base	    qword at rsp+function_args_size		; .base is effective address of threadproc
label .runtimeHost  qword at rsp+function_args_size+8		; .runtimeHost is address of ICLRRuntimeHost

	mov	qword [.base], rdx				; save .base address

	mov	rax, qword [rcx + PROCPARAMS.LoadLibraryA]	; rcx point to PROCPARAMS struct
	lea	rcx, qword [rdx + mscoree - threadproc] 	; now rcx is real address of mscoree
	call	rax						; call LoadLibraryA
	test	rax, rax					; check for errors
	jnz	@f
	inc	rax						; early exit
	jmp	.exit
@@:

	mov	rcx, rax					; rcx = dll handle and the first param
	mov	rdx, [.base]
	add	rdx, CorBindToRuntimeEx - threadproc		; now rdx is real address of "CorBindToRuntimeEx" string and the second param

	mov	rax, qword [.param]
	call	qword [rax + PROCPARAMS.GetProcAddress] 	; call GetProcAddress
	test	rax, rax					; check for errors
	jnz	@f
	inc	rax
	jmp	.exit

@@:

	; HRESULT hr = ::CorBindToRuntimeEx(runtime, L"wks", 0, CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, &runtimeHost);

	lea	rdx, qword [.runtimeHost]			; the 6th arg: address of .runtimeHost
	mov	qword [rsp + 5*8], rdx

	mov	rdx, [.base]

	lea	rcx, [rdx + IID_ICLRRuntimeHost - threadproc]	; now rcx is real address of IID_ICLRRuntimeHost
	mov	qword [rsp + 4*8], rcx				; the 5th arg: address of IID_ICLRRuntimeHost

	lea	r9, [rdx + CLSID_CLRRuntimeHost - threadproc]	; the 4th arg: address of CLSID_CLRRuntimeHost

	xor	r8, r8						; the 3rd arg: flags
	mov	r8, 14h

	lea	rdx, [rdx + wks - threadproc]			; the 2nd arg: address of 'wks'

	mov	rcx, qword [.param]
	mov	rcx, qword [rcx + PROCPARAMS.runtime]		; the 1st arg: address of runtime version string

	call	rax						; call CorBindToRuntimeEx
	failed	eax, .exit

	; runtimeHost->Start()
	com64	.runtimeHost, ICLRRuntimeHost.Start
	failed	eax, .exit

	; hr = runtimeHost->ExecuteInDefaultAppDomain(dllPath, className, functionName, param, &dwRet);
	xor	rax, rax
	mov	qword [esp+5*8], rax				; the 5tg arg: zero
	mov	qword [esp+4*8], rax				; the 4tg arg: zero

	mov	rax, [.param]
	mov	r9, qword [rax + PROCPARAMS.functionName]	; the 3rd arg: functionName
	mov	r8, qword [rax + PROCPARAMS.className]		; the 2rd arg: className
	mov	rdx, qword [rax + PROCPARAMS.dllPath]		; the 1rd arg: dllPath

	com64	.runtimeHost, ICLRRuntimeHost.ExecuteInDefaultAppDomain
	failed	eax, .exit

	com64	.runtimeHost, ICLRRuntimeHost.Release
	failed	eax, .exit

	xor	rax, rax					; success
.exit:

	add	rsp, stack_frame
	pop	rdi

	retn

include 'datadef.inc'
