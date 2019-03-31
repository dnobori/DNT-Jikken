# Makefile

OPTIONS_COMPILE_RELEASE=-DNDEBUG -DVPN_SPEED -D_REENTRANT -DREENTRANT -D_THREAD_SAFE -D_THREADSAFE -DTHREAD_SAFE -DTHREADSAFE -D_FILE_OFFSET_BITS=64 -fsigned-char -g -O1 -fno-stack-protector

OPTIONS_LINK_RELEASE=-fsigned-char -g -O1 -fno-stack-protector

OPTIONS_COMPILE=$(OPTIONS_COMPILE_RELEASE)
OPTIONS_LINK=$(OPTIONS_LINK_RELEASE)

CC=gcc


# Build Action
default:	build

build:	bin/test32 disasm_linux/test32.asm disasm_linux/test_asm_32.asm bin/test64 disasm_linux/test64.asm disasm_linux/test_asm_64.asm obj_win32/test_asm_32.obj obj_win32/test_asm_64.obj disasm_win32/test_asm_32.asm disasm_win32/test_asm_64.asm

disasm_linux/test32.asm: bin/test32
	@mkdir -p disasm_linux/
	objdump -dlSrD bin/test32 > disasm_linux/test32.asm

disasm_linux/test_asm_32.asm: obj_linux/test_asm_32.o
	@mkdir -p disasm_linux/
	objdump -dlSrD obj_linux/test_asm_32.o > disasm_linux/test_asm_32.asm

disasm_linux/test64.asm: bin/test64
	@mkdir -p disasm_linux/
	objdump -dlSrD bin/test64 > disasm_linux/test64.asm

disasm_linux/test_asm_64.asm: obj_linux/test_asm_64.o
	@mkdir -p disasm_linux/
	objdump -dlSrD obj_linux/test_asm_64.o > disasm_linux/test_asm_64.asm

disasm_win32/test_asm_32.asm: obj_win32/test_asm_32.obj
	@mkdir -p disasm_win32/
	objdump -dlSrD obj_linux/test_asm_32.o > disasm_win32/test_asm_32.asm

disasm_win32/test_asm_64.asm: obj_win32/test_asm_64.obj
	@mkdir -p disasm_win32/
	objdump -dlSrD obj_linux/test_asm_64.o > disasm_win32/test_asm_64.asm


obj_linux/test32.o: test.c test.h
	@mkdir -p obj_linux/
	@mkdir -p bin/
	$(CC) -m32 $(OPTIONS_COMPILE) -c test.c -o obj_linux/test32.o

obj_linux/test64.o: test.c test.h
	@mkdir -p obj_linux/
	@mkdir -p bin/
	$(CC) -m64 $(OPTIONS_COMPILE) -c test.c -o obj_linux/test64.o

obj_linux/test_asm_32.o: test_asm_32.s
	@mkdir -p obj_linux/
	@mkdir -p bin/
	as -32 -c test_asm_32.s -o obj_linux/test_asm_32.o

obj_linux/test_asm_64.o: test_asm_64.s
	@mkdir -p obj_linux/
	@mkdir -p bin/
	as -64 -c test_asm_64.s -o obj_linux/test_asm_64.o

obj_win32/test_asm_32.obj: obj_linux/test_asm_32.o
	@mkdir -p obj_win32/
	objcopy obj_linux/test_asm_32.o --output-format=pe-i386 --prefix-symbols _ obj_win32/test_asm_32.obj

obj_win32/test_asm_64.obj: obj_linux/test_asm_64.o
	@mkdir -p obj_win32/
	objcopy obj_linux/test_asm_64.o --output-format=pe-x86-64 obj_win32/test_asm_64.obj

bin/test32: obj_linux/test32.o obj_linux/test_asm_32.o
	$(CC) -m32 obj_linux/test32.o obj_linux/test_asm_32.o $(OPTIONS_LINK) -o bin/test32

bin/test64: obj_linux/test64.o obj_linux/test_asm_64.o
	$(CC) -m64 obj_linux/test64.o obj_linux/test_asm_64.o $(OPTIONS_LINK) -o bin/test64

clean:
	-rm -f obj_linux/*
	-rm -f bin/test32 bin/test64

help:
	@echo "make"
	@echo "make install"
	@echo "make clean"


