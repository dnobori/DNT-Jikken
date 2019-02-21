# Makefile

OPTIONS_COMPILE_RELEASE=-DNDEBUG -DVPN_SPEED -D_REENTRANT -DREENTRANT -D_THREAD_SAFE -D_THREADSAFE -DTHREAD_SAFE -DTHREADSAFE -D_FILE_OFFSET_BITS=64 -fsigned-char -g -O1 -fno-stack-protector

OPTIONS_LINK_RELEASE=-fsigned-char -g -O1 -fno-stack-protector

OBJECTS_test=obj/obj/linux/test64.o obj/obj/linux/code64.o

OPTIONS_COMPILE=$(OPTIONS_COMPILE_RELEASE)
OPTIONS_LINK=$(OPTIONS_LINK_RELEASE)

CC=gcc


# Build Action
default:	build

build:	$(OBJECTS_test) bin/test64 bin/test_lib64.asm bin/test_exec64.asm

bin/test_lib64.asm: obj/obj/linux/test64.o
	objdump -dlSr obj/obj/linux/test64.o > bin/test_lib64.asm

bin/test_exec64.asm: bin/test64
	objdump -dlSr bin/test64 > bin/test_exec64.asm

obj/obj/linux/test64.o: test.c common.h
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) -m64 $(OPTIONS_COMPILE) -c test.c -o obj/obj/linux/test64.o

obj/obj/linux/code64.o: GeneratedCode.c common.h
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) -m64 $(OPTIONS_COMPILE) -c GeneratedCode.c -o obj/obj/linux/code64.o

obj/obj/linux/c2asm_func2.o: c2asm_func2.s
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	as -64 -c c2asm_func2.s -o obj/obj/linux/c2asm_func2.o

obj/obj/linux/c2asm_func2_vc.o: obj/obj/linux/c2asm_func2.o
	objcopy obj/obj/linux/c2asm_func2.o --output-format=pe-bigobj-x86-64 obj/obj/linux/c2asm_func2_vc.o

bin/test64: obj/obj/linux/test64.o obj/obj/linux/code64.o obj/obj/linux/c2asm_func2.o obj/obj/linux/c2asm_func2_vc.o
	$(CC) -m64 obj/obj/linux/test64.o obj/obj/linux/code64.o obj/obj/linux/c2asm_func2.o $(OPTIONS_LINK) -o bin/test64

clean:
	-rm -f $(OBJECTS_test)
	-rm -f obj/obj/linux/*
	-rm -f bin/test bin/test64

help:
	@echo "make"
	@echo "make install"
	@echo "make clean"


