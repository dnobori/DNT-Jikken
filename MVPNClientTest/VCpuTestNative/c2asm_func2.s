	.file	"test.c"
	.text
	.globl	c2asm_func2
	.type	c2asm_func2, @function
c2asm_func2:
.LFB30:
	.cfi_startproc
	imull	$42698888, (%rcx), %edx
	imull	$81920000, 4(%rcx), %eax
	addl	%edx, %eax
	movl	%eax, 8(%rcx)
	ret
	.cfi_endproc
.LFE30:
	.size	c2asm_func2, .-c2asm_func2
	