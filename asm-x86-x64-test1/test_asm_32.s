	.text
	.globl	asm_sum

asm_sum:
    push %ebp
    mov %esp, %ebp
    mov $0, %eax
    add 8(%ebp), %eax
    add 12(%ebp), %eax
    pop %ebp
	ret




