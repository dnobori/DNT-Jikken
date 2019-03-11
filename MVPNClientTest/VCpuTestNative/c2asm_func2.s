# .include "dynasm_include.s"

	.text
	.globl	c2asm_func2
	.type	c2asm_func2, @function

	.globl	test_get_fs_register
	.type	test_get_fs_register, @function

	.globl	test_get_gs_register
	.type	test_get_gs_register, @function

	.globl	test_set_fs_register
	.type	test_set_fs_register, @function

	.globl	test_set_gs_register
	.type	test_set_gs_register, @function

	.globl	asm_read_memory_with_fs
	.type	asm_read_memory_with_fs, @function

	.globl	asm_read_memory_with_gs
	.type	asm_read_memory_with_gs, @function
	
	.globl	asm_write_memory_with_fs
	.type	asm_write_memory_with_fs, @function

	.globl	asm_write_memory_with_gs
	.type	asm_write_memory_with_gs, @function

c2asm_func2:
	.cfi_startproc
	movq	%rcx, %r8
#	imull	$42698888, C2ASM_a(%rcx), %edx
#	imull	$81920000, C2ASM_b(%rcx), %eax
	addl	%edx, %eax
aa:
	mov $0x1122334455667788, %r15
	mov %eax, %r12d
	shr %r12d
	and $0xFF, %r12d
	mov 123(%eax, %r11d, 4), %r11d
	mov 0(%rip), %r11
#	movl	%eax, C2ASM_c(%rcx)
#	SET_D_MEM	C2ASM_f(%rcx)
	cmpl	%eax, %eax
	pushfq
	movl	(%rsp), %r9d
#	SET_D	%r9d
	popfq
	ret
	.cfi_endproc

asm_read_memory_with_fs:
	.cfi_startproc
	mov %fs:(%rcx), %rax
	ret
	.cfi_endproc

asm_read_memory_with_gs:
	.cfi_startproc
	mov %gs:(%rcx), %rax
	ret
	.cfi_endproc

asm_write_memory_with_fs:
	.cfi_startproc
	mov %rdx, %fs:(%rcx)
	ret
	.cfi_endproc

asm_write_memory_with_gs:
	.cfi_startproc
	mov %rdx, %gs:(%rcx)
	ret
	.cfi_endproc

test_get_fs_register:
	.cfi_startproc
	rdfsbase %rax
	ret
	.cfi_endproc

test_get_gs_register:
	.cfi_startproc
	rdgsbase %rax
	ret
	.cfi_endproc


test_set_fs_register:
	.cfi_startproc
	wrfsbase %rcx
	ret
	.cfi_endproc

test_set_gs_register:
	.cfi_startproc
	wrgsbase %rcx
	ret
	.cfi_endproc


