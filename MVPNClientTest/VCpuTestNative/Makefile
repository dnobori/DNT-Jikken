# Makefile

OPTIONS_COMPILE_RELEASE=-DNDEBUG -DVPN_SPEED -D_REENTRANT -DREENTRANT -D_THREAD_SAFE -D_THREADSAFE -DTHREAD_SAFE -DTHREADSAFE -D_FILE_OFFSET_BITS=64 -fsigned-char -g -O1 -fno-stack-protector

OPTIONS_LINK_RELEASE=-fsigned-char -g -O1 -fno-stack-protector

OBJECTS_test=obj/obj/linux/test64.o obj/obj/linux/code64.o

OPTIONS_COMPILE=$(OPTIONS_COMPILE_RELEASE)
OPTIONS_LINK=$(OPTIONS_LINK_RELEASE)

CC=gcc


# Build Action
default:	build

build:	$(OBJECTS_test) bin/test64 bin/test_lib64.asm bin/test_exec64.asm bin/dynasm_o.asm bin/dynasm_vc_o.asm

#bin/nativetest_x64_exe.asm: bin/nativetest_x64.exe
#	objdump -dlSrD bin/nativetest_x64.exe > bin/nativetest_x64_exe.asm

bin/test_lib64.asm: obj/obj/linux/test64.o
	objdump -dlSrD obj/obj/linux/test64.o > bin/test_lib64.asm

bin/dynasm_o.asm: obj/obj/linux/dynasm.o
	objdump -dlSrD obj/obj/linux/dynasm.o > bin/dynasm_o.asm

bin/dynasm_vc_o.asm: obj/obj/linux/dynasm_vc.o
	objdump -dlSrD obj/obj/linux/dynasm_vc.o > bin/dynasm_vc_o.asm

bin/test_exec64.asm: bin/test64
	objdump -dlSrD bin/test64 > bin/test_exec64.asm

obj/obj/linux/test64.o: test.c common.h
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) -m64 $(OPTIONS_COMPILE) -c test.c -o obj/obj/linux/test64.o

obj/obj/linux/code64.o: GeneratedCode.c common.h
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	$(CC) -m64 $(OPTIONS_COMPILE) -c GeneratedCode.c -o obj/obj/linux/code64.o

obj/obj/linux/c2asm_func2.o: c2asm_func2.s dynasm_include.s
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	as -64 -c c2asm_func2.s -o obj/obj/linux/c2asm_func2.o

obj/obj/linux/c2asm_func2_vc.o: obj/obj/linux/c2asm_func2.o
	objcopy obj/obj/linux/c2asm_func2.o --output-format=pe-bigobj-x86-64 obj/obj/linux/c2asm_func2_vc.o

obj/obj/linux/dynasm.o: dynasm.s dynasm_include.s
	@mkdir -p obj/obj/linux/
	@mkdir -p bin/
	as -64 -c dynasm.s -o obj/obj/linux/dynasm.o

obj/obj/linux/dynasm_vc.o: obj/obj/linux/dynasm.o
	objcopy obj/obj/linux/dynasm.o --output-format=pe-bigobj-x86-64 obj/obj/linux/dynasm_vc.o

bin/test64: obj/obj/linux/test64.o obj/obj/linux/code64.o obj/obj/linux/c2asm_func2.o obj/obj/linux/c2asm_func2_vc.o obj/obj/linux/dynasm.o obj/obj/linux/dynasm_vc.o
	$(CC) -m64 obj/obj/linux/test64.o obj/obj/linux/code64.o obj/obj/linux/c2asm_func2.o obj/obj/linux/dynasm.o $(OPTIONS_LINK) -o bin/test64

clean:
	-rm -f $(OBJECTS_test)
	-rm -f obj/obj/linux/*
	-rm -f bin/test bin/test64

help:
	@echo "make"
	@echo "make install"
	@echo "make clean"


