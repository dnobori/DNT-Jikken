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
    .globl L_8048910
    .globl L_8048911
    .globl L_8048912
    .globl L_8048918
    .globl L_8048920
    .globl L_8048924
    .globl L_8048926
    .globl L_8048928
    .globl L_804892c
    .globl L_804892e
    .globl L_8048930
    .globl L_8048934
    .globl L_8048937
    .globl L_804893a
    .globl L_804893c
    .globl L_804893e
    .globl L_8048943
    .globl L_8048945
    .globl L_8048948
    .globl L_804894c
    .globl L_804894e
    .globl L_8048950
    .globl L_8048954
    .globl L_8048956
    .globl L_8048959
    .globl L_8048960
    .globl L_8048964
    .globl L_8048967
    .globl L_804896a
    .globl L_804896c
    .globl L_804896e
    .globl L_8048971
    .globl L_8048973
    .globl L_8048979
    .globl L_804897a
    .globl L_804897b
    .globl L_804897c
    .globl L_8048980
    .globl L_8048981
    .globl L_8048982
    .globl L_8048985
    .globl L_8048989
    .globl L_804898b
    .globl L_804898d
    .globl L_8048990
    .globl L_8048992
    .globl L_8048994
    .globl L_8048996
    .globl L_8048999
    .globl L_80489a0
    .globl L_80489a3
    .globl L_80489a5
    .globl L_80489a8
    .globl L_80489ab
    .globl L_80489ac
    .globl L_80489b1
    .globl L_80489b4
    .globl L_80489b6
    .globl L_80489b9
    .globl L_80489bb
    .globl L_80489be
    .globl L_80489c0
    .globl L_80489c1
    .globl L_80489c2
    .globl L_80489c3
    .globl L_80489c4
    .globl L_80489c8
    .globl L_80489cb
    .globl L_80489ce
    .globl L_80489d0
    .globl L_80489d1
    .globl L_80489d2
    .globl L_80489d3
    .globl L_80489d5
    .globl L_80489d7
    .globl L_80489dc
    .globl L_80489de
    .globl L_80489e0
    .globl L_80489e3
    .globl L_80489eb
    .globl L_80489ef
    .globl L_80489f0
    .globl L_80489f5
    .globl L_80489f8
    .globl L_80489f9
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
cmp $0x8048910, %r13d
jb L_JUMP_TABLE_INVALID_ADDRESS
cmp $0x80489f9, %r13d
ja L_JUMP_TABLE_INVALID_ADDRESS
add $127, %al
sahf
lea -0x8048910(%r13d), %r14d
lea 7(%rip), %r12
mov (%r12, %r14, 8), %r14
jmp %r14
L_JUMP_TABLE_DATA:
.quad L_8048910
.quad L_8048911
.quad L_8048912
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048918
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048920
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048924
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048926
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048928
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804892c
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804892e
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048930
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048934
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048937
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804893a
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804893c
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804893e
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048943
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048945
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048948
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804894c
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804894e
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048950
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048954
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048956
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048959
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048960
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048964
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048967
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804896a
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804896c
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804896e
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048971
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048973
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048979
.quad L_804897a
.quad L_804897b
.quad L_804897c
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048980
.quad L_8048981
.quad L_8048982
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048985
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048989
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804898b
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_804898d
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048990
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048992
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048994
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048996
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_8048999
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489a0
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489a3
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489a5
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489a8
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489ab
.quad L_80489ac
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489b1
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489b4
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489b6
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489b9
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489bb
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489be
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489c0
.quad L_80489c1
.quad L_80489c2
.quad L_80489c3
.quad L_80489c4
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489c8
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489cb
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489ce
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489d0
.quad L_80489d1
.quad L_80489d2
.quad L_80489d3
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489d5
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489d7
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489dc
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489de
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489e0
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489e3
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489eb
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489ef
.quad L_80489f0
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489f5
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_JUMP_TABLE_INVALID_ADDRESS2
.quad L_80489f8
.quad L_80489f9
L_JUMP_TABLE_INVALID_ADDRESS:
add $127, %al
sahf
L_JUMP_TABLE_INVALID_ADDRESS2:
movl $InvalidJumpTarget, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
movl %r13d, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
jmp L_ERROR

L_8048910:
# 8048910 push %esi
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048910
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048910
mov %esi, (%r13)


L_8048911:
# 8048911 push %ebx
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048911
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048911
mov %ebx, (%r13)


L_8048912:
# 8048912 sub $0x1f50,%esp
sub $0x1f50, %r11d

L_8048918:
# 8048918 movl $0x7d0,0xc(%esp)
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048918
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048918
movl $0x7d0, (%r12)


L_8048920:
# 8048920 mov 0xc(%esp),%eax
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048920
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048920
movl (%r12), %r10d


L_8048924:
# 8048924 test %eax,%eax
test %r10d, %r10d

L_8048926:
# 8048926 je 804893e <test_target2+0x2e>
je L_804893e;

L_8048928:
# 8048928 lea 0x10(%esp),%ebx
lea 0x10(%r11d), %ebx

L_804892c:
# 804892c xor %eax,%eax
xor %r10d, %r10d

L_804892e:
# 804892e xchg %ax,%ax

L_8048930:
# 8048930 mov 0xc(%esp),%edx
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048930
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048930
movl (%r12), %r9d


L_8048934:
# 8048934 mov %eax,(%ebx,%eax,4)
lea 0x0(%ebx, %r10d, 4), %r12d
lea (%r12, %r15, 1), %r12
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048934
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048934
movl %r10d, (%r12)


L_8048937:
# 8048937 add $0x1,%eax
add $0x1, %r10d

L_804893a:
# 804893a cmp %eax,%edx
cmp %r10d, %r9d

L_804893c:
# 804893c ja 8048930 <test_target2+0x20>
ja L_8048930;

L_804893e:
# 804893e mov $0xc350,%esi
movl $0xc350, %esi

L_8048943:
# 8048943 xor %eax,%eax
xor %r10d, %r10d

L_8048945:
# 8048945 lea 0x0(%esi),%esi
lea 0x0(%esi), %esi

L_8048948:
# 8048948 mov 0xc(%esp),%edx
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048948
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048948
movl (%r12), %r9d


L_804894c:
# 804894c test %edx,%edx
test %r9d, %r9d

L_804894e:
# 804894e je 804896e <test_target2+0x5e>
je L_804896e;

L_8048950:
# 8048950 lea 0x10(%esp),%ebx
lea 0x10(%r11d), %ebx

L_8048954:
# 8048954 xor %edx,%edx
xor %r9d, %r9d

L_8048956:
# 8048956 lea 0x0(%esi),%esi
lea 0x0(%esi), %esi

L_8048959:
# 8048959 lea 0x0(%edi,%eiz,1),%edi
lea 0x0(%edi), %edi

L_8048960:
# 8048960 mov 0xc(%esp),%ecx
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048960
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048960
movl (%r12), %r14d


L_8048964:
# 8048964 add (%ebx,%edx,4),%eax
lea 0x0(%ebx, %r9d, 4), %r12d
lea (%r12, %r15, 1), %r12
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048964
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048964
add (%r12), %r10d


L_8048967:
# 8048967 add $0x1,%edx
add $0x1, %r9d

L_804896a:
# 804896a cmp %edx,%ecx
cmp %r9d, %r14d

L_804896c:
# 804896c ja 8048960 <test_target2+0x50>
ja L_8048960;

L_804896e:
# 804896e sub $0x1,%esi
sub $0x1, %esi

L_8048971:
# 8048971 jne 8048948 <test_target2+0x38>
jne L_8048948;

L_8048973:
# 8048973 add $0x1f50,%esp
add $0x1f50, %r11d

L_8048979:
# 8048979 pop %ebx
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048979
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048979
mov (%r13), %ebx

lea 4(%r11d), %r11d

L_804897a:
# 804897a pop %esi
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_804897a
cmp %r8, %r15
je L_ERROR_OUT_RANGE_804897a
mov (%r13), %esi

lea 4(%r11d), %r11d

L_804897b:
# 804897b ret 
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_804897b
cmp %r8, %r15
je L_ERROR_OUT_RANGE_804897b
mov (%r13), %ecx

lea 4(%r11d), %r11d
mov %ecx, %r13d
jmp L_JUMP_TABLE

L_804897c:
# 804897c lea 0x0(%esi,%eiz,1),%esi
lea 0x0(%esi), %esi

L_8048980:
# 8048980 push %esi
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048980
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048980
mov %esi, (%r13)


L_8048981:
# 8048981 push %ebx
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048981
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048981
mov %ebx, (%r13)


L_8048982:
# 8048982 sub $0x4,%esp
sub $0x4, %r11d

L_8048985:
# 8048985 mov 0x10(%esp),%ebx
lea 0x10(%r11d), %r12d
lea (%r12, %r15, 1), %r12
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_8048985
cmp %r8, %r15
je L_ERROR_OUT_RANGE_8048985
movl (%r12), %ebx


L_8048989:
# 8048989 test %ebx,%ebx
test %ebx, %ebx

L_804898b:
# 804898b je 80489d3 <test_target4+0x53>
je L_80489d3;

L_804898d:
# 804898d cmp $0x1,%ebx
cmp $0x1, %ebx

L_8048990:
# 8048990 je 80489d7 <test_target4+0x57>
je L_80489d7;

L_8048992:
# 8048992 xor %esi,%esi
xor %esi, %esi

L_8048994:
# 8048994 jmp 80489a5 <test_target4+0x25>
jmp L_80489a5;

L_8048996:
# 8048996 lea 0x0(%esi),%esi
lea 0x0(%esi), %esi

L_8048999:
# 8048999 lea 0x0(%edi,%eiz,1),%edi
lea 0x0(%edi), %edi

L_80489a0:
# 80489a0 cmp $0x1,%ebx
cmp $0x1, %ebx

L_80489a3:
# 80489a3 je 80489c8 <test_target4+0x48>
je L_80489c8;

L_80489a5:
# 80489a5 lea -0x1(%ebx),%eax
lea -0x1(%ebx), %r10d

L_80489a8:
# 80489a8 sub $0xc,%esp
sub $0xc, %r11d

L_80489ab:
# 80489ab push %eax
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489ab
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489ab
mov %r10d, (%r13)


L_80489ac:
# 80489ac call 8048980 <test_target4>
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489ac
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489ac
movl $0x80489b1, (%r13)

jmp L_8048980;

L_80489b1:
# 80489b1 add $0x10,%esp
add $0x10, %r11d

L_80489b4:
# 80489b4 add %eax,%esi
add %r10d, %esi

L_80489b6:
# 80489b6 sub $0x2,%ebx
sub $0x2, %ebx

L_80489b9:
# 80489b9 jne 80489a0 <test_target4+0x20>
jne L_80489a0;

L_80489bb:
# 80489bb add $0x4,%esp
add $0x4, %r11d

L_80489be:
# 80489be mov %esi,%eax
movl %esi, %r10d

L_80489c0:
# 80489c0 pop %ebx
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489c0
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489c0
mov (%r13), %ebx

lea 4(%r11d), %r11d

L_80489c1:
# 80489c1 pop %esi
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489c1
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489c1
mov (%r13), %esi

lea 4(%r11d), %r11d

L_80489c2:
# 80489c2 ret 
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489c2
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489c2
mov (%r13), %ecx

lea 4(%r11d), %r11d
mov %ecx, %r13d
jmp L_JUMP_TABLE

L_80489c3:
# 80489c3 nop 

L_80489c4:
# 80489c4 lea 0x0(%esi,%eiz,1),%esi
lea 0x0(%esi), %esi

L_80489c8:
# 80489c8 add $0x1,%esi
add $0x1, %esi

L_80489cb:
# 80489cb add $0x4,%esp
add $0x4, %r11d

L_80489ce:
# 80489ce mov %esi,%eax
movl %esi, %r10d

L_80489d0:
# 80489d0 pop %ebx
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489d0
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489d0
mov (%r13), %ebx

lea 4(%r11d), %r11d

L_80489d1:
# 80489d1 pop %esi
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489d1
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489d1
mov (%r13), %esi

lea 4(%r11d), %r11d

L_80489d2:
# 80489d2 ret 
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489d2
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489d2
mov (%r13), %ecx

lea 4(%r11d), %r11d
mov %ecx, %r13d
jmp L_JUMP_TABLE

L_80489d3:
# 80489d3 xor %esi,%esi
xor %esi, %esi

L_80489d5:
# 80489d5 jmp 80489bb <test_target4+0x3b>
jmp L_80489bb;

L_80489d7:
# 80489d7 mov $0x1,%esi
movl $0x1, %esi

L_80489dc:
# 80489dc jmp 80489bb <test_target4+0x3b>
jmp L_80489bb;

L_80489de:
# 80489de xchg %ax,%ax

L_80489e0:
# 80489e0 sub $0x28,%esp
sub $0x28, %r11d

L_80489e3:
# 80489e3 movl $0x22,0x18(%esp)
lea 0x18(%r11d), %r12d
lea (%r12, %r15, 1), %r12
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489e3
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489e3
movl $0x22, (%r12)


L_80489eb:
# 80489eb mov 0x18(%esp),%eax
lea 0x18(%r11d), %r12d
lea (%r12, %r15, 1), %r12
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489eb
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489eb
movl (%r12), %r10d


L_80489ef:
# 80489ef push %eax
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489ef
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489ef
mov %r10d, (%r13)


L_80489f0:
# 80489f0 call 8048980 <test_target4>
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489f0
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489f0
movl $0x80489f5, (%r13)

jmp L_8048980;

L_80489f5:
# 80489f5 add $0x2c,%esp
add $0x2c, %r11d

L_80489f8:
# 80489f8 ret 
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
cmp %rsp, %r15
je L_ERROR_OUT_RANGE_80489f8
cmp %r8, %r15
je L_ERROR_OUT_RANGE_80489f8
mov (%r13), %ecx

lea 4(%r11d), %r11d
mov %ecx, %r13d
jmp L_JUMP_TABLE

L_80489f9:
# 80489f9 lea 0x0(%esi,%eiz,1),%esi
lea 0x0(%esi), %esi

L_ERROR_OUT_RANGE_8048910:
movl $0x8048910, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048911:
movl $0x8048911, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048918:
movl $0x8048918, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048920:
movl $0x8048920, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048930:
movl $0x8048930, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048934:
movl $0x8048934, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048948:
movl $0x8048948, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048960:
movl $0x8048960, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048964:
movl $0x8048964, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048979:
movl $0x8048979, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_804897a:
movl $0x804897a, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_804897b:
movl $0x804897b, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048980:
movl $0x8048980, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048981:
movl $0x8048981, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_8048985:
movl $0x8048985, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489ab:
movl $0x80489ab, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489ac:
movl $0x80489ac, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489c0:
movl $0x80489c0, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489c1:
movl $0x80489c1, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489c2:
movl $0x80489c2, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489d0:
movl $0x80489d0, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489d1:
movl $0x80489d1, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489d2:
movl $0x80489d2, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489e3:
movl $0x80489e3, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489eb:
movl $0x80489eb, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489ef:
movl $0x80489ef, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489f0:
movl $0x80489f0, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR
L_ERROR_OUT_RANGE_80489f8:
movl $0x80489f8, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)
movl $2, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)
jmp L_ERROR



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

