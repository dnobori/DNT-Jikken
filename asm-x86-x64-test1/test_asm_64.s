	.text
	.globl	asm_sum
	.globl	asm_test1

asm_sum:
    mov $0, %rax
    add %rcx, %rax
    add %rdx, %rax
#    add $1000, %rax
	ret

asm_test1:
    lea 0x7fffffff(%rcx), %rbx
    lea 0(%ecx, %edx, 2), %rbx
    mov %rbx, %rax
    ret

