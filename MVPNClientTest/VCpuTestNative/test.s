	.file	"test.c"
	.text
	.globl	Tick64
	.type	Tick64, @function
Tick64:
.LFB23:
	.cfi_startproc
	subq	$40, %rsp
	.cfi_def_cfa_offset 48
	movq	%fs:40, %rax
	movq	%rax, 24(%rsp)
	xorl	%eax, %eax
	movq	$0, (%rsp)
	movq	$0, 8(%rsp)
	movq	%rsp, %rsi
	movl	$1, %edi
	call	clock_gettime
	movl	(%rsp), %eax
	imulq	$1000, %rax, %rcx
	movabsq	$4835703278458516699, %rdx
	movq	%rdx, %rax
	mulq	8(%rsp)
	shrq	$18, %rdx
	leaq	(%rcx,%rdx), %rax
	movq	24(%rsp), %rsi
	xorq	%fs:40, %rsi
	je	.L2
	call	__stack_chk_fail
.L2:
	addq	$40, %rsp
	.cfi_def_cfa_offset 8
	ret
	.cfi_endproc
.LFE23:
	.size	Tick64, .-Tick64
	.globl	ToStr64
	.type	ToStr64, @function
ToStr64:
.LFB24:
	.cfi_startproc
	subq	$280, %rsp
	.cfi_def_cfa_offset 288
	movq	%rdi, %r8
	movq	%fs:40, %rax
	movq	%rax, 264(%rsp)
	xorl	%eax, %eax
	movb	$0, (%rsp)
	movq	%rsp, %r9
	movq	%rsp, %rcx
	movl	$0, %r10d
	movabsq	$-3689348814741910323, %r11
.L5:
	movq	%rsi, %rax
	mulq	%r11
	shrq	$3, %rdx
	movq	%rdx, %rdi
	leaq	(%rdx,%rdx,4), %rax
	addq	%rax, %rax
	subq	%rax, %rsi
	movq	%rsi, %rdx
	movq	%rdi, %rsi
	addl	$1, %r10d
	addl	$48, %edx
	movb	%dl, (%rcx)
	addq	$1, %rcx
	testq	%rdi, %rdi
	jne	.L5
	movl	%r10d, %r10d
	movb	$0, (%rsp,%r10)
	movq	%rsp, %rdi
	movl	$0, %eax
	movq	$-1, %rcx
	repnz scasb
	movq	%rcx, %rax
	notq	%rax
	leaq	-1(%rax), %rsi
	testl	%esi, %esi
	je	.L6
	leal	-1(%rsi), %eax
.L7:
	movl	%eax, %edx
	movzbl	(%r9), %ecx
	movb	%cl, (%r8,%rdx)
	subl	$1, %eax
	addq	$1, %r9
	cmpl	$-1, %eax
	jne	.L7
.L6:
	movl	%esi, %esi
	movb	$0, (%r8,%rsi)
	movq	264(%rsp), %rax
	xorq	%fs:40, %rax
	je	.L8
	call	__stack_chk_fail
.L8:
	addq	$280, %rsp
	.cfi_def_cfa_offset 8
	ret
	.cfi_endproc
.LFE24:
	.size	ToStr64, .-ToStr64
	.globl	ToStr3
	.type	ToStr3, @function
ToStr3:
.LFB25:
	.cfi_startproc
	pushq	%rbp
	.cfi_def_cfa_offset 16
	.cfi_offset 6, -16
	pushq	%rbx
	.cfi_def_cfa_offset 24
	.cfi_offset 3, -24
	subq	$280, %rsp
	.cfi_def_cfa_offset 304
	movq	%rdi, %rbx
	movq	%fs:40, %rax
	movq	%rax, 264(%rsp)
	xorl	%eax, %eax
	movq	%rsp, %rdi
	call	ToStr64
	movl	$0, %eax
	movq	$-1, %rcx
	movq	%rsp, %rdi
	repnz scasb
	movq	%rcx, %rax
	notq	%rax
	leaq	-1(%rax), %rdi
	movl	%edi, %r9d
	movl	%edi, %eax
	subl	$1, %eax
	js	.L22
	leaq	128(%rsp), %rdx
	movl	$0, %ecx
.L14:
	addl	$1, %ecx
	movl	%eax, %esi
	movzbl	(%rsp,%rsi), %esi
	movb	%sil, (%rdx)
	addq	$1, %rdx
	subl	$1, %eax
	jns	.L14
	jmp	.L13
.L22:
	movl	$0, %ecx
.L13:
	movl	%ecx, %ecx
	movb	$0, 128(%rsp,%rcx)
	testl	%edi, %edi
	je	.L23
	leaq	128(%rsp), %r8
	movl	$0, %ecx
	movl	$0, %edi
	movl	$-1431655765, %r10d
	jmp	.L16
.L18:
	testl	%ecx, %ecx
	je	.L17
	movl	%ecx, %eax
	mull	%r10d
	shrl	%edx
	leal	(%rdx,%rdx,2), %eax
	cmpl	%eax, %ecx
	jne	.L17
	movl	%esi, %esi
	movb	$44, (%rsp,%rsi)
	leal	2(%rdi), %esi
.L17:
	addq	$1, %r8
	movl	%esi, %edi
.L16:
	leal	1(%rdi), %esi
	movl	%edi, %eax
	movzbl	(%r8), %edx
	movb	%dl, (%rsp,%rax)
	addl	$1, %ecx
	cmpl	%ecx, %r9d
	jne	.L18
	jmp	.L15
.L23:
	movl	$0, %esi
.L15:
	movl	%esi, %esi
	movb	$0, (%rsp,%rsi)
	movq	%rsp, %rdi
	movl	$0, %eax
	movq	$-1, %rcx
	repnz scasb
	notq	%rcx
	movl	%ecx, %eax
	subl	$2, %eax
	js	.L24
	leaq	128(%rsp), %rdx
	movl	$0, %ecx
.L20:
	addl	$1, %ecx
	movl	%eax, %esi
	movzbl	(%rsp,%rsi), %esi
	movb	%sil, (%rdx)
	addq	$1, %rdx
	subl	$1, %eax
	jns	.L20
	jmp	.L19
.L24:
	movl	$0, %ecx
.L19:
	movl	%ecx, %ecx
	movb	$0, 128(%rsp,%rcx)
	leaq	128(%rsp), %rsi
	movq	%rbx, %rdi
	call	strcpy
	movq	264(%rsp), %rax
	xorq	%fs:40, %rax
	je	.L21
	call	__stack_chk_fail
.L21:
	addq	$280, %rsp
	.cfi_def_cfa_offset 24
	popq	%rbx
	.cfi_def_cfa_offset 16
	popq	%rbp
	.cfi_def_cfa_offset 8
	ret
	.cfi_endproc
.LFE25:
	.size	ToStr3, .-ToStr3
	.section	.rodata.str1.1,"aMS",@progbits,1
.LC0:
	.string	"Access violation to 0x%x."
	.text
	.globl	mem_write
	.type	mem_write, @function
mem_write:
.LFB26:
	.cfi_startproc
	movl	%esi, %ecx
	andl	$4095, %ecx
	movl	%esi, %eax
	shrl	$12, %eax
	movl	%eax, %eax
	salq	$4, %rax
	addq	%rax, %rdi
	cmpl	$0, 12(%rdi)
	jne	.L29
	subq	$8, %rsp
	.cfi_def_cfa_offset 16
	movl	%esi, %edx
	movl	$.LC0, %esi
	movl	$1, %edi
	movl	$0, %eax
	call	__printf_chk
	movl	$0, %edi
	call	exit
.L29:
	.cfi_def_cfa_offset 8
	movl	%ecx, %ecx
	movq	(%rdi), %rax
	movl	%edx, (%rax,%rcx)
	ret
	.cfi_endproc
.LFE26:
	.size	mem_write, .-mem_write
	.globl	mem_read
	.type	mem_read, @function
mem_read:
.LFB27:
	.cfi_startproc
	movl	%esi, %edx
	andl	$4095, %edx
	movl	%esi, %eax
	shrl	$12, %eax
	movl	%eax, %eax
	salq	$4, %rax
	addq	%rax, %rdi
	cmpl	$0, 12(%rdi)
	jne	.L32
	subq	$8, %rsp
	.cfi_def_cfa_offset 16
	movl	%esi, %edx
	movl	$.LC0, %esi
	movl	$1, %edi
	movl	$0, %eax
	call	__printf_chk
	movl	$0, %edi
	call	exit
.L32:
	.cfi_def_cfa_offset 8
	movl	%edx, %edx
	movq	(%rdi), %rax
	movl	(%rax,%rdx), %eax
	ret
	.cfi_endproc
.LFE27:
	.size	mem_read, .-mem_read
	.globl	AllignMemoryToPage
	.type	AllignMemoryToPage, @function
AllignMemoryToPage:
.LFB28:
	.cfi_startproc
	movl	%edi, %eax
	shrl	$12, %eax
	movl	%eax, (%rdx)
	addl	%edi, %esi
	shrl	$12, %esi
	subl	%eax, %esi
	movl	%esi, (%rcx)
	ret
	.cfi_endproc
.LFE28:
	.size	AllignMemoryToPage, .-AllignMemoryToPage
	.section	.rodata.str1.8,"aMS",@progbits,1
	.align 8
.LC1:
	.string	"Page number %u, count %u already exists.\n"
	.text
	.globl	AllocateMemory
	.type	AllocateMemory, @function
AllocateMemory:
.LFB29:
	.cfi_startproc
	pushq	%r15
	.cfi_def_cfa_offset 16
	.cfi_offset 15, -16
	pushq	%r14
	.cfi_def_cfa_offset 24
	.cfi_offset 14, -24
	pushq	%r13
	.cfi_def_cfa_offset 32
	.cfi_offset 13, -32
	pushq	%r12
	.cfi_def_cfa_offset 40
	.cfi_offset 12, -40
	pushq	%rbp
	.cfi_def_cfa_offset 48
	.cfi_offset 6, -48
	pushq	%rbx
	.cfi_def_cfa_offset 56
	.cfi_offset 3, -56
	subq	$24, %rsp
	.cfi_def_cfa_offset 80
	movl	%esi, %ebx
	shrl	$12, %ebx
	leal	(%rsi,%rdx), %r12d
	shrl	$12, %r12d
	movl	%r12d, %eax
	subl	%ebx, %eax
	movl	%eax, 12(%rsp)
	cmpl	%r12d, %ebx
	jnb	.L35
	movl	%r8d, %r15d
	movl	%ecx, %r14d
	movq	%rdi, %r13
	movl	%ebx, %ebp
.L38:
	movl	%ebp, %eax
	salq	$4, %rax
	addq	0(%r13), %rax
	cmpq	$0, (%rax)
	je	.L37
	movl	12(%rsp), %ecx
	movl	%ebx, %edx
	movl	$.LC1, %esi
	movl	$1, %edi
	movl	$0, %eax
	call	__printf_chk
.L37:
	addl	$1, %ebp
	cmpl	%r12d, %ebp
	jb	.L38
.L40:
	movl	%ebx, %ebp
	salq	$4, %rbp
	addq	0(%r13), %rbp
	movl	$4096, %edi
	call	malloc
	movq	%rax, 0(%rbp)
	movl	%r14d, 8(%rbp)
	movl	%r15d, 12(%rbp)
	addl	$1, %ebx
	cmpl	%r12d, %ebx
	jb	.L40
.L35:
	addq	$24, %rsp
	.cfi_def_cfa_offset 56
	popq	%rbx
	.cfi_def_cfa_offset 48
	popq	%rbp
	.cfi_def_cfa_offset 40
	popq	%r12
	.cfi_def_cfa_offset 32
	popq	%r13
	.cfi_def_cfa_offset 24
	popq	%r14
	.cfi_def_cfa_offset 16
	popq	%r15
	.cfi_def_cfa_offset 8
	ret
	.cfi_endproc
.LFE29:
	.size	AllocateMemory, .-AllocateMemory
	.globl	c2asm_func1
	.type	c2asm_func1, @function
c2asm_func1:
.LFB30:
	.cfi_startproc
	imull	$42698888, (%rcx), %edx
	imull	$81920000, 4(%rcx), %eax
	addl	%edx, %eax
	movl	%eax, 8(%rcx)
	ret
	.cfi_endproc
.LFE30:
	.size	c2asm_func1, .-c2asm_func1
	.section	.rodata.str1.1
.LC2:
	.string	"c = %X\n"
	.text
	.globl	c2asm_test1
	.type	c2asm_test1, @function
c2asm_test1:
.LFB31:
	.cfi_startproc
	pushq	%rdi
	.cfi_def_cfa_offset 16
	.cfi_offset 5, -16
	pushq	%rsi
	.cfi_def_cfa_offset 24
	.cfi_offset 4, -24
	subq	$232, %rsp
	.cfi_def_cfa_offset 256
	movaps	%xmm6, 64(%rsp)
	movaps	%xmm7, 80(%rsp)
	movaps	%xmm8, 96(%rsp)
	movaps	%xmm9, 112(%rsp)
	movaps	%xmm10, 128(%rsp)
	movaps	%xmm11, 144(%rsp)
	movaps	%xmm12, 160(%rsp)
	movaps	%xmm13, 176(%rsp)
	movaps	%xmm14, 192(%rsp)
	movaps	%xmm15, 208(%rsp)
	.cfi_offset 23, -192
	.cfi_offset 24, -176
	.cfi_offset 25, -160
	.cfi_offset 26, -144
	.cfi_offset 27, -128
	.cfi_offset 28, -112
	.cfi_offset 29, -96
	.cfi_offset 30, -80
	.cfi_offset 31, -64
	.cfi_offset 32, -48
	movq	%fs:40, %rax
	movq	%rax, 56(%rsp)
	xorl	%eax, %eax
	movl	$0, 40(%rsp)
	movl	$3477777, 32(%rsp)
	movl	$39221111, 36(%rsp)
	leaq	32(%rsp), %rcx
	call	c2asm_func1
	movl	40(%rsp), %edx
	movl	$.LC2, %esi
	movl	$1, %edi
	movl	$0, %eax
	call	__printf_chk
	movq	56(%rsp), %rax
	xorq	%fs:40, %rax
	je	.L46
	call	__stack_chk_fail
.L46:
	movaps	64(%rsp), %xmm6
	movaps	80(%rsp), %xmm7
	movaps	96(%rsp), %xmm8
	movaps	112(%rsp), %xmm9
	movaps	128(%rsp), %xmm10
	movaps	144(%rsp), %xmm11
	movaps	160(%rsp), %xmm12
	movaps	176(%rsp), %xmm13
	movaps	192(%rsp), %xmm14
	movaps	208(%rsp), %xmm15
	addq	$232, %rsp
	.cfi_restore 32
	.cfi_restore 31
	.cfi_restore 30
	.cfi_restore 29
	.cfi_restore 28
	.cfi_restore 27
	.cfi_restore 26
	.cfi_restore 25
	.cfi_restore 24
	.cfi_restore 23
	.cfi_def_cfa_offset 24
	popq	%rsi
	.cfi_restore 4
	.cfi_def_cfa_offset 16
	popq	%rdi
	.cfi_restore 5
	.cfi_def_cfa_offset 8
	ret
	.cfi_endproc
.LFE31:
	.size	c2asm_test1, .-c2asm_test1
	.globl	main
	.type	main, @function
main:
.LFB32:
	.cfi_startproc
	subq	$40, %rsp
	.cfi_def_cfa_offset 48
	call	c2asm_test1
	addq	$40, %rsp
	.cfi_def_cfa_offset 8
	ret
	.cfi_endproc
.LFE32:
	.size	main, .-main
	.ident	"GCC: (Ubuntu 5.4.0-6ubuntu1~16.04.11) 5.4.0 20160609"
	.section	.note.GNU-stack,"",@progbits
