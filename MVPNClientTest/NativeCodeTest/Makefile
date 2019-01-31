# Makefile

OPTIONS_COMPILE_RELEASE=-m32 -DNDEBUG -DVPN_SPEED -D_REENTRANT -DREENTRANT -D_THREAD_SAFE -D_THREADSAFE -DTHREAD_SAFE -DTHREADSAFE -D_FILE_OFFSET_BITS=64 -fsigned-char -g -O3

OPTIONS_LINK_RELEASE=-m32 -fsigned-char -g -O3

OBJECTS_test=obj/obj/linux/test.o bin/test_lib.asm bin/test_exec.asm

OPTIONS_COMPILE=$(OPTIONS_COMPILE_RELEASE)
OPTIONS_LINK=$(OPTIONS_LINK_RELEASE)


# Build Action
default:	build

build:	$(OBJECTS_test) bin/test bin/test_lib.asm bin/test_exec.asm

bin/test_lib.asm: obj/obj/linux/test.o
	objdump -dlSr obj/obj/linux/test.o > bin/test_lib.asm

bin/test_exec.asm: bin/test
	objdump -dlSr bin/test > bin/test_exec.asm

obj/obj/linux/test.o: test.c
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) $(OPTIONS_COMPILE) -c test.c -o obj/obj/linux/test.o

bin/test: obj/obj/linux/test.o
	$(CC) obj/obj/linux/test.o $(OPTIONS_LINK) -o bin/test

clean:
	-rm -f $(OBJECTS_test)
	-rm -f bin/test

help:
	@echo "make"
	@echo "make install"
	@echo "make clean"


