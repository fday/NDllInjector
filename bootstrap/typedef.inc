
macro failed reg, label {
      test   reg, reg
      js     label
}

; comcall macros reserves additional stack space before calling
macro com64 var_pointer, proc {
      mov   rax, qword [var_pointer]
      mov   rcx, rax			   ; arg this
      mov   rax, qword [rax]
      call  qword [rax + proc]		 ; call proc
}

; definition from fasm/usecom.asm example
struc GUID def
 {
   match d1-d2-d3-d4-d5, def
    \{
      .Data1 dd 0x\#d1
      .Data2 dw 0x\#d2
      .Data3 dw 0x\#d3
      .Data4 db 0x\#d4 shr 8,0x\#d4 and 0FFh
      .Data5 db 0x\#d5 shr 40,0x\#d5 shr 32 and 0FFh,0x\#d5 shr 24 and 0FFh,0x\#d5 shr 16 and 0FFh,0x\#d5 shr 8 and 0FFh,0x\#d5 and 0FFh
    \}
 }


interface ICLRRuntimeHost,\
	   QueryInterface,\
	   AddRef,\
	   Release,\
	   Start,\
	   Stop,\
	   SetHostControl,\
	   GetCLRControl,\
	   UnloadAppDomain,\
	   ExecuteInAppDomain,\
	   GetCurrentAppDomainId,\
	   ExecuteApplication,\
	   ExecuteInDefaultAppDomain

