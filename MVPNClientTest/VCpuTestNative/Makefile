# Makefile

OPTIONS_COMPILE_RELEASE=-DNDEBUG -DVPN_SPEED -D_REENTRANT -DREENTRANT -D_THREAD_SAFE -D_THREADSAFE -DTHREAD_SAFE -DTHREADSAFE -D_FILE_OFFSET_BITS=64 -fsigned-char -g -O2 -fno-stack-protector

OPTIONS_LINK_RELEASE=-fsigned-char -g -O2 -fno-stack-protector

OBJECTS_test=obj/obj/linux/test.o obj/obj/linux/test64.o obj/obj/linux/code.o obj/obj/linux/code64.o bin/test_lib.asm bin/test_exec.asm

OPTIONS_COMPILE=$(OPTIONS_COMPILE_RELEASE)
OPTIONS_LINK=$(OPTIONS_LINK_RELEASE)

CC=gcc


# Build Action
default:	build

build:	$(OBJECTS_test) bin/test bin/test64 bin/test_lib.asm bin/test_exec.asm bin/test_lib64.asm bin/test_exec64.asm

bin/test_lib.asm: obj/obj/linux/test.o
	objdump -dlSr obj/obj/linux/test.o > bin/test_lib.asm

bin/test_exec.asm: bin/test
	objdump -dlSr bin/test > bin/test_exec.asm

bin/test_lib64.asm: obj/obj/linux/test64.o
	objdump -dlSr obj/obj/linux/test64.o > bin/test_lib64.asm

bin/test_exec64.asm: bin/test64
	objdump -dlSr bin/test64 > bin/test_exec64.asm

obj/obj/linux/test.o: test.c common.h
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) -m32 $(OPTIONS_COMPILE) -c test.c -o obj/obj/linux/test.o

obj/obj/linux/test64.o: test.c common.h
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) -m64 $(OPTIONS_COMPILE) -c test.c -o obj/obj/linux/test64.o

obj/obj/linux/code.o: GeneratedCode.c common.h
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) -m32 $(OPTIONS_COMPILE) -c GeneratedCode.c -o obj/obj/linux/code.o

obj/obj/linux/code64.o: GeneratedCode.c common.h
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) -m64 $(OPTIONS_COMPILE) -c GeneratedCode.c -o obj/obj/linux/code64.o

bin/test: obj/obj/linux/test.o obj/obj/linux/code.o
	$(CC) -m32 obj/obj/linux/test.o obj/obj/linux/code.o $(OPTIONS_LINK) -o bin/test

bin/test64: obj/obj/linux/test64.o obj/obj/linux/code64.o
	$(CC) -m64 obj/obj/linux/test64.o obj/obj/linux/code64.o $(OPTIONS_LINK) -o bin/test64

clean:
	-rm -f $(OBJECTS_test)
	-rm -f bin/test bin/test64

help:
	@echo "make"
	@echo "make install"
	@echo "make clean"


