# Makefile

OPTIONS_COMPILE_RELEASE=-DNDEBUG -DVPN_SPEED -D_REENTRANT -DREENTRANT -D_THREAD_SAFE -D_THREADSAFE -DTHREAD_SAFE -DTHREADSAFE -D_FILE_OFFSET_BITS=64 -fsigned-char -g -O2 -fno-stack-protector

OPTIONS_LINK_RELEASE=-fsigned-char -g -O2 -fno-stack-protector

OBJECTS_test=obj/obj/linux/test32.o obj/obj/linux/test64.o bin/test_lib32.asm bin/test_exec32.asm bin/test_lib64.asm bin/test_exec64.asm

OPTIONS_COMPILE=$(OPTIONS_COMPILE_RELEASE)
OPTIONS_LINK=$(OPTIONS_LINK_RELEASE)


# Build Action
default:	build

build:	$(OBJECTS_test) bin/test32 bin/test64 bin/test_lib32.asm bin/test_exec32.asm bin/test_lib64.asm bin/test_exec64.asm

bin/test_lib32.asm: obj/obj/linux/test32.o
	objdump -dlSr obj/obj/linux/test32.o > bin/test_lib32.asm

bin/test_exec32.asm: bin/test32
	objdump -dlSr bin/test32 > bin/test_exec32.asm

bin/test_lib64.asm: obj/obj/linux/test64.o
	objdump -dlSr obj/obj/linux/test64.o > bin/test_lib64.asm

bin/test_exec64.asm: bin/test64
	objdump -dlSr bin/test64 > bin/test_exec64.asm

obj/obj/linux/test32.o: test.c
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) -m32 $(OPTIONS_COMPILE) -c test.c -o obj/obj/linux/test32.o

obj/obj/linux/test64.o: test.c
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) -m64 $(OPTIONS_COMPILE) -c test.c -o obj/obj/linux/test64.o

bin/test32: obj/obj/linux/test32.o
	$(CC) -m32 obj/obj/linux/test32.o $(OPTIONS_LINK) -o bin/test32

bin/test64: obj/obj/linux/test64.o
	$(CC) -m64 obj/obj/linux/test64.o $(OPTIONS_LINK) -o bin/test64

clean:
	-rm -f $(OBJECTS_test)
	-rm -f bin/test32
	-rm -f bin/test64

help:
	@echo "make"
	@echo "make install"
	@echo "make clean"


