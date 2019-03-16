InvalidJumpTarget = 1
MemoryOutOfRange = 2


.include "dynasm_include.s"

	.text
	.globl	dynasm
	.globl	dynasm_begin
	.globl	dynasm_end
    .globl  L_ERROR
    .globl  L_JUMP_TABLE
    .globl  L_JUMP_TABLE_DATA
    .globl  L_JUMP_TABLE_INVALID_ADDRESS
    .globl  L_JUMP_TABLE_INVALID_ADDRESS2
#    .globl	ASM_GLOBAL_CPU_STATE
#    .globl  ASM_GLOBAL_CONT_MEM
    .globl L_a70
    .globl L_a71
    .globl L_a72
    .globl L_a74
    .globl L_a79
    .globl L_a7c
    .globl L_a84
    .globl L_a88
    .globl L_a8b
    .globl L_a8d
    .globl L_a90
    .globl L_a93
    .globl L_a95
    .globl L_a98
    .globl L_a9a
    .globl L_a9f
    .globl L_aa1
    .globl L_aa8
    .globl L_aaa
    .globl L_aac
    .globl L_aae
    .globl L_ab0
    .globl L_ab2
    .globl L_ab5
    .globl L_ab7
    .globl L_ab9
    .globl L_abc
    .globl L_ac0
    .globl L_ac3
    .globl L_ac5
    .globl L_ac7
    .globl L_aca
    .globl L_acc
    .globl L_acd
    .globl L_ace
    .globl L_acf
    .globl L_ad0
    .globl L_ad1
    .globl L_ad2
    .globl L_ad8
    .globl L_ae0
    .globl L_ae4
    .globl L_ae6
    .globl L_ae8
    .globl L_aec
    .globl L_aee
    .globl L_af0
    .globl L_af4
    .globl L_af7
    .globl L_afa
    .globl L_afc
    .globl L_afe
    .globl L_b03
    .globl L_b05
    .globl L_b08
    .globl L_b0c
    .globl L_b0e
    .globl L_b12
    .globl L_b14
    .globl L_b16
    .globl L_b19
    .globl L_b20
    .globl L_b24
    .globl L_b27
    .globl L_b2a
    .globl L_b2c
    .globl L_b2e
    .globl L_b31
    .globl L_b33
    .globl L_b39
    .globl L_b3a
    .globl L_b3b
    .globl L_b3c
    .globl L_b40
    .globl L_b41
    .globl L_b42
    .globl L_b45
    .globl L_b49
    .globl L_b4b
    .globl L_b4d
    .globl L_b50
    .globl L_b52
    .globl L_b54
    .globl L_b57
    .globl L_b5a
    .globl L_b5b
    .globl L_b60
    .globl L_b63
    .globl L_b65
    .globl L_b68
    .globl L_b6a
    .globl L_b6d
    .globl L_b6f
    .globl L_b72
    .globl L_b75
    .globl L_b77
    .globl L_b78
    .globl L_b79
    .globl L_b7a
    .globl L_b80
    .globl L_b82
    .globl L_b85
    .globl L_b87
    .globl L_b88
    .globl L_b89
    .globl L_b8a
    .globl L_b90
    .globl L_b95
    .globl L_b98
    .globl L_b9a
    .globl L_b9b
    .globl L_b9c
    .globl L_b9d
    .globl L_ba0
    .globl L_ba3
    .globl L_bab
    .globl L_baf
    .globl L_bb0
    .globl L_bb5
    .globl L_bb8
    .globl L_bb9
.type	dynasm, @function
dynasm:
	.cfi_startproc
	push	%r12
	push	%r13
	push	%r14
	push	%r15
	push	%rdi
	push	%rsi
	push	%rbx
	push	%rbp
	lea 	-16*10(%rsp), %rsp
	vmovdqu	%xmm6, 16*0(%rsp)
	vmovdqu	%xmm7, 16*1(%rsp)
	vmovdqu	%xmm8, 16*2(%rsp)
	vmovdqu	%xmm9, 16*3(%rsp)
	vmovdqu	%xmm10, 16*4(%rsp)
	vmovdqu	%xmm11, 16*5(%rsp)
	vmovdqu	%xmm12, 16*6(%rsp)
	vmovdqu	%xmm13, 16*7(%rsp)
	vmovdqu	%xmm14, 16*8(%rsp)
	vmovdqu	%xmm15, 16*9(%rsp)

    mov     %rcx, %r8

    movabs  $ASM_GLOBAL_CONT_MEM, %r15
    subl     DYNASM_CPU_STATE_CONT_START(%r8), %r15d
    # mov     DYNASM_CPU_STATE_CONT_MEM_MINUS_START(%r8), %r15

    mov     DYNASM_CPU_STATE_START_IP(%r8), %r13d

    movl DYNASM_CPU_STATE_EAX(%r8), %r10d
    movl DYNASM_CPU_STATE_EBX(%r8), %ebx
    movl DYNASM_CPU_STATE_ECX(%r8), %r14d
    movl DYNASM_CPU_STATE_EDX(%r8), %r9d
    movl DYNASM_CPU_STATE_ESI(%r8), %esi
    movl DYNASM_CPU_STATE_EDI(%r8), %edi
    movl DYNASM_CPU_STATE_ESP(%r8), %r11d
    movl DYNASM_CPU_STATE_EBP(%r8), %ebp
dynasm_begin:

	# -- BEGIN --

            movabs $0xfaceface, %r12
            pushq %r12
            pushq %r12


L_JUMP_TABLE:
# Jump table
mov $0, %eax
seto %al
lahf
cmp $0x7fffffff, %r13d
jne L_RESUME
add $127, %al
sahf
jmp L_ERROR
L_RESUME:
cmp $0xa70, %r13d
jb L_JUMP_TABLE_INVALID_ADDRESS
cmp $0xbb9, %r13d
ja L_JUMP_TABLE_INVALID_ADDRESS
add $127, %al
sahf
lea -0xa70(%r13d), %r14d
lea 7(%rip), %r12
mov (%r12, %r14, 8), %r14
jmp %r14
L_JUMP_TABLE_DATA:
.quad L_a70
.quad L_a71
.quad L_a72
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a74
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a79
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a7c
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a84
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a88
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a8b
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a8d
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a90
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a93
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a95
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a98
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a9a
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_a9f
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_aa1
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_aa8
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_aaa
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_aac
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_aae
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ab0
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ab2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ab5
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ab7
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ab9
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_abc
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ac0
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ac3
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ac5
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ac7
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_aca
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_acc
.quad L_acd
.quad L_ace
.quad L_acf
.quad L_ad0
.quad L_ad1
.quad L_ad2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ad8
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ae0
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ae4
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ae6
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ae8
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_aec
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_aee
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_af0
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_af4
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_af7
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_afa
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_afc
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_afe
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b03
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b05
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b08
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b0c
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b0e
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b12
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b14
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b16
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b19
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b20
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b24
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b27
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b2a
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b2c
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b2e
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b31
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b33
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b39
.quad L_b3a
.quad L_b3b
.quad L_b3c
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b40
.quad L_b41
.quad L_b42
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b45
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b49
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b4b
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b4d
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b50
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b52
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b54
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b57
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b5a
.quad L_b5b
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b60
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b63
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b65
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b68
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b6a
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b6d
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b6f
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b72
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b75
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b77
.quad L_b78
.quad L_b79
.quad L_b7a
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b80
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b82
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b85
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b87
.quad L_b88
.quad L_b89
.quad L_b8a
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b90
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b95
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b98
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_b9a
.quad L_b9b
.quad L_b9c
.quad L_b9d
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ba0
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_ba3
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_bab
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_baf
.quad L_bb0
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_bb5
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_bb8
.quad L_bb9
L_JUMP_TABLE_INVALID_ADDRESS:
add $127, %al
sahf
L_JUMP_TABLE_INVALID_ADDRESS2:
movl $InvalidJumpTarget, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
movl %r13d, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
jmp L_ERROR

L_a70:
# a70 push %esi
lea -4(%r11d), %r11d
mov %esi, (%r11, %r15, 1)


L_a71:
# a71 push %ebx
lea -4(%r11d), %r11d
mov %ebx, (%r11, %r15, 1)


L_a72:
# a72 xor %esi,%esi
xor %esi, %esi

L_a74:
# a74 mov $0x3,%ebx
movl $0x3, %ebx

L_a79:
# a79 sub $0x10,%esp
sub $0x10, %r11d

L_a7c:
# a7c movl $0x4e20,0xc(%esp)
lea 0xC(%r11d), %r12d
movl $0x4e20, (%r12, %r15, 1)


L_a84:
# a84 mov 0xc(%esp),%eax
lea 0xC(%r11d), %r12d
movl (%r12, %r15, 1), %r10d


L_a88:
# a88 cmp $0x2,%eax
cmp $0x2, %r10d

L_a8b:
# a8b jbe ac7 <test_target1+0x57>
jbe L_ac7;

L_a8d:
# a8d lea 0x0(%esi),%esi
lea 0x0(%esi), %esi

L_a90:
# a90 cmp $0x2,%ebx
cmp $0x2, %ebx

L_a93:
# a93 jbe ab9 <test_target1+0x49>
jbe L_ab9;

L_a95:
# a95 test $0x1,%bl
test $0x1, %bl

L_a98:
# a98 je abc <test_target1+0x4c>
je L_abc;

L_a9a:
# a9a mov $0x2,%ecx
movl $0x2, %r14d

L_a9f:
# a9f jmp ab2 <test_target1+0x42>
jmp L_ab2;

L_aa1:
# aa1 lea 0x0(%esi,%eiz,1),%esi
lea 0x0(%esi), %esi

L_aa8:
# aa8 xor %edx,%edx
xor %r9d, %r9d

L_aaa:
# aaa mov %ebx,%eax
movl %ebx, %r10d

L_aac:
# aac div %ecx
mov %r10d, %eax
mov %r9d, %edx
div %r14d
mov %eax, %r10d
mov %edx, %r9d

L_aae:
# aae test %edx,%edx
test %r9d, %r9d

L_ab0:
# ab0 je abc <test_target1+0x4c>
je L_abc;

L_ab2:
# ab2 add $0x1,%ecx
add $0x1, %r14d

L_ab5:
# ab5 cmp %ebx,%ecx
cmp %ebx, %r14d

L_ab7:
# ab7 jne aa8 <test_target1+0x38>
jne L_aa8;

L_ab9:
# ab9 add $0x1,%esi
add $0x1, %esi

L_abc:
# abc mov 0xc(%esp),%eax
lea 0xC(%r11d), %r12d
movl (%r12, %r15, 1), %r10d


L_ac0:
# ac0 add $0x1,%ebx
add $0x1, %ebx

L_ac3:
# ac3 cmp %ebx,%eax
cmp %ebx, %r10d

L_ac5:
# ac5 jae a90 <test_target1+0x20>
jae L_a90;

L_ac7:
# ac7 add $0x10,%esp
add $0x10, %r11d

L_aca:
# aca mov %esi,%eax
movl %esi, %r10d

L_acc:
# acc pop %ebx
mov (%r11, %r15, 1), %ebx

lea 4(%r11d), %r11d

L_acd:
# acd pop %esi
mov (%r11, %r15, 1), %esi

lea 4(%r11d), %r11d

L_ace:
# ace ret 
mov (%r11, %r15, 1), %ecx

lea 4(%r11d), %r11d
mov $0, %eax
seto %al
lahf
mov %ecx, %r13d
cmp $0x7fffffff, %r13d
je L_ERROR
popq %rcx
popq %rdx
cmpl $0xfaceface, %ecx
je L_HELPER1_ace
cmp %edx, %r13d
jne L_JUMP_TABLE
add $127, %al
sahf
jmp %rcx
L_HELPER1_ace:
movabs $0xfaceface, %r12
pushq %r12
pushq %r12
add $127, %al
sahf
jmp L_JUMP_TABLE

L_acf:
# acf nop 

L_ad0:
# ad0 push %esi
lea -4(%r11d), %r11d
mov %esi, (%r11, %r15, 1)


L_ad1:
# ad1 push %ebx
lea -4(%r11d), %r11d
mov %ebx, (%r11, %r15, 1)


L_ad2:
# ad2 sub $0x1f50,%esp
sub $0x1f50, %r11d

L_ad8:
# ad8 movl $0x7d0,0xc(%esp)
lea 0xC(%r11d), %r12d
movl $0x7d0, (%r12, %r15, 1)


L_ae0:
# ae0 mov 0xc(%esp),%eax
lea 0xC(%r11d), %r12d
movl (%r12, %r15, 1), %r10d


L_ae4:
# ae4 test %eax,%eax
test %r10d, %r10d

L_ae6:
# ae6 je afe <test_target2+0x2e>
je L_afe;

L_ae8:
# ae8 lea 0x10(%esp),%ebx
lea 0x10(%r11d), %ebx

L_aec:
# aec xor %eax,%eax
xor %r10d, %r10d

L_aee:
# aee xchg %ax,%ax

L_af0:
# af0 mov 0xc(%esp),%edx
lea 0xC(%r11d), %r12d
movl (%r12, %r15, 1), %r9d


L_af4:
# af4 mov %eax,(%ebx,%eax,4)
lea 0x0(%ebx, %r10d, 4), %r12d
movl %r10d, (%r12, %r15, 1)


L_af7:
# af7 add $0x1,%eax
add $0x1, %r10d

L_afa:
# afa cmp %eax,%edx
cmp %r10d, %r9d

L_afc:
# afc ja af0 <test_target2+0x20>
ja L_af0;

L_afe:
# afe mov $0xc350,%esi
movl $0xc350, %esi

L_b03:
# b03 xor %eax,%eax
xor %r10d, %r10d

L_b05:
# b05 lea 0x0(%esi),%esi
lea 0x0(%esi), %esi

L_b08:
# b08 mov 0xc(%esp),%ecx
lea 0xC(%r11d), %r12d
movl (%r12, %r15, 1), %r14d


L_b0c:
# b0c xor %edx,%edx
xor %r9d, %r9d

L_b0e:
# b0e lea 0x10(%esp),%ebx
lea 0x10(%r11d), %ebx

L_b12:
# b12 test %ecx,%ecx
test %r14d, %r14d

L_b14:
# b14 je b2e <test_target2+0x5e>
je L_b2e;

L_b16:
# b16 lea 0x0(%esi),%esi
lea 0x0(%esi), %esi

L_b19:
# b19 lea 0x0(%edi,%eiz,1),%edi
lea 0x0(%edi), %edi

L_b20:
# b20 mov 0xc(%esp),%ecx
lea 0xC(%r11d), %r12d
movl (%r12, %r15, 1), %r14d


L_b24:
# b24 add (%ebx,%edx,4),%eax
lea 0x0(%ebx, %r9d, 4), %r12d
add (%r12, %r15, 1), %r10d


L_b27:
# b27 add $0x1,%edx
add $0x1, %r9d

L_b2a:
# b2a cmp %edx,%ecx
cmp %r9d, %r14d

L_b2c:
# b2c ja b20 <test_target2+0x50>
ja L_b20;

L_b2e:
# b2e sub $0x1,%esi
sub $0x1, %esi

L_b31:
# b31 jne b08 <test_target2+0x38>
jne L_b08;

L_b33:
# b33 add $0x1f50,%esp
add $0x1f50, %r11d

L_b39:
# b39 pop %ebx
mov (%r11, %r15, 1), %ebx

lea 4(%r11d), %r11d

L_b3a:
# b3a pop %esi
mov (%r11, %r15, 1), %esi

lea 4(%r11d), %r11d

L_b3b:
# b3b ret 
mov (%r11, %r15, 1), %ecx

lea 4(%r11d), %r11d
mov $0, %eax
seto %al
lahf
mov %ecx, %r13d
cmp $0x7fffffff, %r13d
je L_ERROR
popq %rcx
popq %rdx
cmpl $0xfaceface, %ecx
je L_HELPER1_b3b
cmp %edx, %r13d
jne L_JUMP_TABLE
add $127, %al
sahf
jmp %rcx
L_HELPER1_b3b:
movabs $0xfaceface, %r12
pushq %r12
pushq %r12
add $127, %al
sahf
jmp L_JUMP_TABLE

L_b3c:
# b3c lea 0x0(%esi,%eiz,1),%esi
lea 0x0(%esi), %esi

L_b40:
# b40 push %esi
lea -4(%r11d), %r11d
mov %esi, (%r11, %r15, 1)


L_b41:
# b41 push %ebx
lea -4(%r11d), %r11d
mov %ebx, (%r11, %r15, 1)


L_b42:
# b42 sub $0x4,%esp
sub $0x4, %r11d

L_b45:
# b45 mov 0x10(%esp),%ebx
lea 0x10(%r11d), %r12d
movl (%r12, %r15, 1), %ebx


L_b49:
# b49 test %ebx,%ebx
test %ebx, %ebx

L_b4b:
# b4b je b80 <test_target4+0x40>
je L_b80;

L_b4d:
# b4d cmp $0x1,%ebx
cmp $0x1, %ebx

L_b50:
# b50 je b90 <test_target4+0x50>
je L_b90;

L_b52:
# b52 xor %esi,%esi
xor %esi, %esi

L_b54:
# b54 lea -0x1(%ebx),%eax
lea -0x1(%ebx), %r10d

L_b57:
# b57 sub $0xc,%esp
sub $0xc, %r11d

L_b5a:
# b5a push %eax
lea -4(%r11d), %r11d
mov %r10d, (%r11, %r15, 1)


L_b5b:
# b5b call b40 <test_target4>
lea -4(%r11d), %r11d
movl $0xb60, (%r11, %r15, 1)

movabs $L_b60, %r13
pushq $0xb60
pushq %r13
jmp L_b40;

L_b60:
# b60 add $0x10,%esp
add $0x10, %r11d

L_b63:
# b63 add %eax,%esi
add %r10d, %esi

L_b65:
# b65 sub $0x2,%ebx
sub $0x2, %ebx

L_b68:
# b68 je b72 <test_target4+0x32>
je L_b72;

L_b6a:
# b6a cmp $0x1,%ebx
cmp $0x1, %ebx

L_b6d:
# b6d jne b54 <test_target4+0x14>
jne L_b54;

L_b6f:
# b6f add $0x1,%esi
add $0x1, %esi

L_b72:
# b72 add $0x4,%esp
add $0x4, %r11d

L_b75:
# b75 mov %esi,%eax
movl %esi, %r10d

L_b77:
# b77 pop %ebx
mov (%r11, %r15, 1), %ebx

lea 4(%r11d), %r11d

L_b78:
# b78 pop %esi
mov (%r11, %r15, 1), %esi

lea 4(%r11d), %r11d

L_b79:
# b79 ret 
mov (%r11, %r15, 1), %ecx

lea 4(%r11d), %r11d
mov $0, %eax
seto %al
lahf
mov %ecx, %r13d
cmp $0x7fffffff, %r13d
je L_ERROR
popq %rcx
popq %rdx
cmpl $0xfaceface, %ecx
je L_HELPER1_b79
cmp %edx, %r13d
jne L_JUMP_TABLE
add $127, %al
sahf
jmp %rcx
L_HELPER1_b79:
movabs $0xfaceface, %r12
pushq %r12
pushq %r12
add $127, %al
sahf
jmp L_JUMP_TABLE

L_b7a:
# b7a lea 0x0(%esi),%esi
lea 0x0(%esi), %esi

L_b80:
# b80 xor %esi,%esi
xor %esi, %esi

L_b82:
# b82 add $0x4,%esp
add $0x4, %r11d

L_b85:
# b85 mov %esi,%eax
movl %esi, %r10d

L_b87:
# b87 pop %ebx
mov (%r11, %r15, 1), %ebx

lea 4(%r11d), %r11d

L_b88:
# b88 pop %esi
mov (%r11, %r15, 1), %esi

lea 4(%r11d), %r11d

L_b89:
# b89 ret 
mov (%r11, %r15, 1), %ecx

lea 4(%r11d), %r11d
mov $0, %eax
seto %al
lahf
mov %ecx, %r13d
cmp $0x7fffffff, %r13d
je L_ERROR
popq %rcx
popq %rdx
cmpl $0xfaceface, %ecx
je L_HELPER1_b89
cmp %edx, %r13d
jne L_JUMP_TABLE
add $127, %al
sahf
jmp %rcx
L_HELPER1_b89:
movabs $0xfaceface, %r12
pushq %r12
pushq %r12
add $127, %al
sahf
jmp L_JUMP_TABLE

L_b8a:
# b8a lea 0x0(%esi),%esi
lea 0x0(%esi), %esi

L_b90:
# b90 mov $0x1,%esi
movl $0x1, %esi

L_b95:
# b95 add $0x4,%esp
add $0x4, %r11d

L_b98:
# b98 mov %esi,%eax
movl %esi, %r10d

L_b9a:
# b9a pop %ebx
mov (%r11, %r15, 1), %ebx

lea 4(%r11d), %r11d

L_b9b:
# b9b pop %esi
mov (%r11, %r15, 1), %esi

lea 4(%r11d), %r11d

L_b9c:
# b9c ret 
mov (%r11, %r15, 1), %ecx

lea 4(%r11d), %r11d
mov $0, %eax
seto %al
lahf
mov %ecx, %r13d
cmp $0x7fffffff, %r13d
je L_ERROR
popq %rcx
popq %rdx
cmpl $0xfaceface, %ecx
je L_HELPER1_b9c
cmp %edx, %r13d
jne L_JUMP_TABLE
add $127, %al
sahf
jmp %rcx
L_HELPER1_b9c:
movabs $0xfaceface, %r12
pushq %r12
pushq %r12
add $127, %al
sahf
jmp L_JUMP_TABLE

L_b9d:
# b9d lea 0x0(%esi),%esi
lea 0x0(%esi), %esi

L_ba0:
# ba0 sub $0x28,%esp
sub $0x28, %r11d

L_ba3:
# ba3 movl $0x22,0x18(%esp)
lea 0x18(%r11d), %r12d
movl $0x22, (%r12, %r15, 1)


L_bab:
# bab mov 0x18(%esp),%eax
lea 0x18(%r11d), %r12d
movl (%r12, %r15, 1), %r10d


L_baf:
# baf push %eax
lea -4(%r11d), %r11d
mov %r10d, (%r11, %r15, 1)


L_bb0:
# bb0 call b40 <test_target4>
lea -4(%r11d), %r11d
movl $0xbb5, (%r11, %r15, 1)

movabs $L_bb5, %r13
pushq $0xbb5
pushq %r13
jmp L_b40;

L_bb5:
# bb5 add $0x2c,%esp
add $0x2c, %r11d

L_bb8:
# bb8 ret 
mov (%r11, %r15, 1), %ecx

lea 4(%r11d), %r11d
mov $0, %eax
seto %al
lahf
mov %ecx, %r13d
cmp $0x7fffffff, %r13d
je L_ERROR
popq %rcx
popq %rdx
cmpl $0xfaceface, %ecx
je L_HELPER1_bb8
cmp %edx, %r13d
jne L_JUMP_TABLE
add $127, %al
sahf
jmp %rcx
L_HELPER1_bb8:
movabs $0xfaceface, %r12
pushq %r12
pushq %r12
add $127, %al
sahf
jmp L_JUMP_TABLE

L_bb9:
# bb9 lea 0x0(%esi,%eiz,1),%esi
lea 0x0(%esi), %esi




	# -- end --

L_ERROR:

dynasm_end:
    movl %r10d, DYNASM_CPU_STATE_EAX(%r8)
    movl %ebx, DYNASM_CPU_STATE_EBX(%r8)
    movl %r14d, DYNASM_CPU_STATE_ECX(%r8)
    movl %r9d, DYNASM_CPU_STATE_EDX(%r8)
    movl %esi, DYNASM_CPU_STATE_ESI(%r8)
    movl %edi, DYNASM_CPU_STATE_EDI(%r8)
    movl %r11d, DYNASM_CPU_STATE_ESP(%r8)
    movl %ebp, DYNASM_CPU_STATE_EBP(%r8)

	vmovdqu	16*9(%rsp), %xmm15
	vmovdqu	16*8(%rsp), %xmm14
	vmovdqu	16*7(%rsp), %xmm13
	vmovdqu	16*6(%rsp), %xmm12
	vmovdqu	16*5(%rsp), %xmm11
	vmovdqu	16*4(%rsp), %xmm10
	vmovdqu	16*3(%rsp), %xmm9
	vmovdqu	16*2(%rsp), %xmm8
	vmovdqu	16*1(%rsp), %xmm7
	vmovdqu	16*0(%rsp), %xmm6

    popq %r12
    popq %r12

	lea		16*10(%rsp), %rsp
	pop	%rbp
	pop	%rbx
	pop	%rsi
	pop	%rdi
	pop	%r15
	pop	%r14
	pop	%r13
	pop	%r12
	ret
	.cfi_endproc

#ASM_GLOBAL_CPU_STATE:
#   .space 1032
#
#ASM_GLOBAL_CONT_MEM:
#    .space 0x200000

