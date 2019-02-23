
.include "dynasm_include.s"

	.text
	.globl	dynasm
	.globl	dynasm_begin
	.globl	dynasm_end
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

    mov     DYNASM_CPU_STATE_CONT_MEM_MINUS_START(%r8), %r15

dynasm_begin:

	# -- BEGIN --


L_80488b0:
# 80488b0 push %esi
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
mov %esi, %r13d
mov %r13d, (%r13)

L_80488b1:
# 80488b1 push %ebx
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
mov %ebx, %r13d
mov %r13d, (%r13)

L_80488b2:
# 80488b2 xor %esi,%esi

L_80488b4:
# 80488b4 mov $0x3,%ebx
mov $0x3, %ebx

L_80488b9:
# 80488b9 sub $0x10,%esp

L_80488bc:
# 80488bc movl $0x4e20,0xc(%esp)
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov $0x4e20, %r12d
mov %r12d, (%r12)

L_80488c4:
# 80488c4 mov 0xc(%esp),%eax
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov (%r12), %r12d
mov %r12d, %r10d

L_80488c8:
# 80488c8 cmp $0x2,%eax

L_80488cb:
# 80488cb jbe 8048907 <test_target1+0x57>

L_80488cd:
# 80488cd lea 0x0(%esi),%esi

L_80488d0:
# 80488d0 cmp $0x2,%ebx

L_80488d3:
# 80488d3 jbe 80488f9 <test_target1+0x49>

L_80488d5:
# 80488d5 test $0x1,%bl

L_80488d8:
# 80488d8 je 80488fc <test_target1+0x4c>

L_80488da:
# 80488da mov $0x2,%ecx
mov $0x2, %r14d

L_80488df:
# 80488df jmp 80488f2 <test_target1+0x42>

L_80488e1:
# 80488e1 lea 0x0(%esi,%eiz,1),%esi

L_80488e8:
# 80488e8 xor %edx,%edx

L_80488ea:
# 80488ea mov %ebx,%eax
mov %ebx, %r10d

L_80488ec:
# 80488ec div %ecx

L_80488ee:
# 80488ee test %edx,%edx

L_80488f0:
# 80488f0 je 80488fc <test_target1+0x4c>

L_80488f2:
# 80488f2 add $0x1,%ecx

L_80488f5:
# 80488f5 cmp %ebx,%ecx

L_80488f7:
# 80488f7 jne 80488e8 <test_target1+0x38>

L_80488f9:
# 80488f9 add $0x1,%esi

L_80488fc:
# 80488fc mov 0xc(%esp),%eax
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov (%r12), %r12d
mov %r12d, %r10d

L_8048900:
# 8048900 add $0x1,%ebx

L_8048903:
# 8048903 cmp %ebx,%eax

L_8048905:
# 8048905 jae 80488d0 <test_target1+0x20>

L_8048907:
# 8048907 add $0x10,%esp

L_804890a:
# 804890a mov %esi,%eax
mov %esi, %r10d

L_804890c:
# 804890c pop %ebx

L_804890d:
# 804890d pop %esi

L_804890e:
# 804890e ret 

L_804890f:
# 804890f nop 

L_8048910:
# 8048910 push %esi
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
mov %esi, %r13d
mov %r13d, (%r13)

L_8048911:
# 8048911 push %ebx
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
mov %ebx, %r13d
mov %r13d, (%r13)

L_8048912:
# 8048912 sub $0x1f50,%esp

L_8048918:
# 8048918 movl $0x7d0,0xc(%esp)
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov $0x7d0, %r12d
mov %r12d, (%r12)

L_8048920:
# 8048920 mov 0xc(%esp),%eax
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov (%r12), %r12d
mov %r12d, %r10d

L_8048924:
# 8048924 test %eax,%eax

L_8048926:
# 8048926 je 804893e <test_target2+0x2e>

L_8048928:
# 8048928 lea 0x10(%esp),%ebx

L_804892c:
# 804892c xor %eax,%eax

L_804892e:
# 804892e xchg %ax,%ax

L_8048930:
# 8048930 mov 0xc(%esp),%edx
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov (%r12), %r12d
mov %r12d, %edx

L_8048934:
# 8048934 mov %eax,(%ebx,%eax,4)
lea 0x0(%ebx, %r10d, 4), %r12d
lea (%r12, %r15, 1), %r12
mov %r10d, %r12d
mov %r12d, (%r12)

L_8048937:
# 8048937 add $0x1,%eax

L_804893a:
# 804893a cmp %eax,%edx

L_804893c:
# 804893c ja 8048930 <test_target2+0x20>

L_804893e:
# 804893e mov $0xc350,%esi
mov $0xc350, %esi

L_8048943:
# 8048943 xor %eax,%eax

L_8048945:
# 8048945 lea 0x0(%esi),%esi

L_8048948:
# 8048948 mov 0xc(%esp),%edx
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov (%r12), %r12d
mov %r12d, %edx

L_804894c:
# 804894c test %edx,%edx

L_804894e:
# 804894e je 804896e <test_target2+0x5e>

L_8048950:
# 8048950 lea 0x10(%esp),%ebx

L_8048954:
# 8048954 xor %edx,%edx

L_8048956:
# 8048956 lea 0x0(%esi),%esi

L_8048959:
# 8048959 lea 0x0(%edi,%eiz,1),%edi

L_8048960:
# 8048960 mov 0xc(%esp),%ecx
lea 0xC(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov (%r12), %r12d
mov %r12d, %r14d

L_8048964:
# 8048964 add (%ebx,%edx,4),%eax

L_8048967:
# 8048967 add $0x1,%edx

L_804896a:
# 804896a cmp %edx,%ecx

L_804896c:
# 804896c ja 8048960 <test_target2+0x50>

L_804896e:
# 804896e sub $0x1,%esi

L_8048971:
# 8048971 jne 8048948 <test_target2+0x38>

L_8048973:
# 8048973 add $0x1f50,%esp

L_8048979:
# 8048979 pop %ebx

L_804897a:
# 804897a pop %esi

L_804897b:
# 804897b ret 

L_804897c:
# 804897c lea 0x0(%esi,%eiz,1),%esi

L_8048980:
# 8048980 push %esi
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
mov %esi, %r13d
mov %r13d, (%r13)

L_8048981:
# 8048981 push %ebx
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
mov %ebx, %r13d
mov %r13d, (%r13)

L_8048982:
# 8048982 sub $0x4,%esp

L_8048985:
# 8048985 mov 0x10(%esp),%ebx
lea 0x10(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov (%r12), %r12d
mov %r12d, %ebx

L_8048989:
# 8048989 test %ebx,%ebx

L_804898b:
# 804898b je 80489d3 <test_target4+0x53>

L_804898d:
# 804898d cmp $0x1,%ebx

L_8048990:
# 8048990 je 80489d7 <test_target4+0x57>

L_8048992:
# 8048992 xor %esi,%esi

L_8048994:
# 8048994 jmp 80489a5 <test_target4+0x25>

L_8048996:
# 8048996 lea 0x0(%esi),%esi

L_8048999:
# 8048999 lea 0x0(%edi,%eiz,1),%edi

L_80489a0:
# 80489a0 cmp $0x1,%ebx

L_80489a3:
# 80489a3 je 80489c8 <test_target4+0x48>

L_80489a5:
# 80489a5 lea -0x1(%ebx),%eax

L_80489a8:
# 80489a8 sub $0xc,%esp

L_80489ab:
# 80489ab push %eax
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
mov %r10d, %r13d
mov %r13d, (%r13)

L_80489ac:
# 80489ac call 8048980 <test_target4>

L_80489b1:
# 80489b1 add $0x10,%esp

L_80489b4:
# 80489b4 add %eax,%esi

L_80489b6:
# 80489b6 sub $0x2,%ebx

L_80489b9:
# 80489b9 jne 80489a0 <test_target4+0x20>

L_80489bb:
# 80489bb add $0x4,%esp

L_80489be:
# 80489be mov %esi,%eax
mov %esi, %r10d

L_80489c0:
# 80489c0 pop %ebx

L_80489c1:
# 80489c1 pop %esi

L_80489c2:
# 80489c2 ret 

L_80489c3:
# 80489c3 nop 

L_80489c4:
# 80489c4 lea 0x0(%esi,%eiz,1),%esi

L_80489c8:
# 80489c8 add $0x1,%esi

L_80489cb:
# 80489cb add $0x4,%esp

L_80489ce:
# 80489ce mov %esi,%eax
mov %esi, %r10d

L_80489d0:
# 80489d0 pop %ebx

L_80489d1:
# 80489d1 pop %esi

L_80489d2:
# 80489d2 ret 

L_80489d3:
# 80489d3 xor %esi,%esi

L_80489d5:
# 80489d5 jmp 80489bb <test_target4+0x3b>

L_80489d7:
# 80489d7 mov $0x1,%esi
mov $0x1, %esi

L_80489dc:
# 80489dc jmp 80489bb <test_target4+0x3b>

L_80489de:
# 80489de xchg %ax,%ax

L_80489e0:
# 80489e0 sub $0x28,%esp

L_80489e3:
# 80489e3 movl $0x22,0x18(%esp)
lea 0x18(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov $0x22, %r12d
mov %r12d, (%r12)

L_80489eb:
# 80489eb mov 0x18(%esp),%eax
lea 0x18(%r11d), %r12d
lea (%r12, %r15, 1), %r12
mov (%r12), %r12d
mov %r12d, %r10d

L_80489ef:
# 80489ef push %eax
lea -4(%r11d), %r11d
mov %r11d, %r13d
lea (%r13, %r15, 1), %r13
mov %r10d, %r13d
mov %r13d, (%r13)

L_80489f0:
# 80489f0 call 8048980 <test_target4>

L_80489f5:
# 80489f5 add $0x2c,%esp

L_80489f8:
# 80489f8 ret 

L_80489f9:
# 80489f9 lea 0x0(%esi,%eiz,1),%esi



	# -- end --

dynasm_end:
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

