
   .data
   .globl	ASM_GLOBAL_CPU_STATE
   .globl  ASM_GLOBAL_CONT_MEM
   .globl  ASM_GLOBAL_CONT_MEM_MINUS_START

ASM_GLOBAL_CPU_STATE:
   .space 1024

ASM_GLOBAL_CONT_MEM:
   .space 0x200000

ASM_GLOBAL_CONT_MEM_MINUS_START:
    .quad ASM_GLOBAL_CONT_MEM - 0x500000

ASM_GLOBAL_TMP1:
    .quad 0x500000
ASM_GLOBAL_TMP2:
    .quad 0x700000
ASM_GLOBAL_TMP3:
    .quad 0
ASM_GLOBAL_TMP4:
    .quad 0
ASM_GLOBAL_TMP5:
    .quad 0
ASM_GLOBAL_TMP6:
    .quad 0
ASM_GLOBAL_TMP7:
    .quad 0
ASM_GLOBAL_TMP8:
    .quad 0

.struct 0
C2ASM_a:
	.struct C2ASM_a + 4
C2ASM_b:
	.struct C2ASM_b + 4
C2ASM_c:
	.struct C2ASM_c + 4
C2ASM_d:
	.struct C2ASM_d + 4
C2ASM_e:
	.struct C2ASM_e + 4
C2ASM_f:
	.struct C2ASM_f + 4


.macro  GET_D   dst_register
    movl    C2ASM_d(%r8), \dst_register
.endm

.macro  GET_D_MEM    dst_address
    GET_D   %r9d
    movl    %r9d,   \dst_address
.endm

.macro  SET_D   src_register
    movl    \src_register,  C2ASM_d(%r8)
.endm

.macro  SET_D_MEM  src_address
    movl    \src_address,   %r9d
    SET_D   %r9d
.endm


.struct 0
DYNASM_CPU_STATE_CONT_MEM_MINUS_START:
    .struct DYNASM_CPU_STATE_CONT_MEM_MINUS_START + 8
DYNASM_CPU_STATE_CONT_START:
    .struct DYNASM_CPU_STATE_CONT_START + 4
DYNASM_CPU_STATE_EAX:
    .struct DYNASM_CPU_STATE_EAX + 4
DYNASM_CPU_STATE_EBX:
    .struct DYNASM_CPU_STATE_EBX + 4
DYNASM_CPU_STATE_ECX:
    .struct DYNASM_CPU_STATE_ECX + 4
DYNASM_CPU_STATE_EDX:
    .struct DYNASM_CPU_STATE_EDX + 4
DYNASM_CPU_STATE_ESI:
    .struct DYNASM_CPU_STATE_ESI + 4
DYNASM_CPU_STATE_EDI:
    .struct DYNASM_CPU_STATE_EDI + 4
DYNASM_CPU_STATE_EBP:
    .struct DYNASM_CPU_STATE_EBP + 4
DYNASM_CPU_STATE_ESP:
    .struct DYNASM_CPU_STATE_ESP + 4
DYNASM_CPU_STATE_START_IP:
    .struct DYNASM_CPU_STATE_START_IP + 4
DYNASM_CPU_STATE_EXCEPTION_ADDRESS:
    .struct DYNASM_CPU_STATE_EXCEPTION_ADDRESS + 4
DYNASM_CPU_STATE_EXCEPTION_TYPE:
    .struct DYNASM_CPU_STATE_EXCEPTION_TYPE + 4




