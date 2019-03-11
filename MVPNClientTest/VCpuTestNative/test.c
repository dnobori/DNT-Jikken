#include <stdio.h>

#ifdef _WIN32
#include <windows.h>
#define NOINLINE	__declspec(noinline)
#else
#include <time.h>
#include <errno.h>
#define NOINLINE	__attribute__((noinline))
#include <asm/prctl.h>
#include <sys/prctl.h>
#endif

#ifndef	_WINDOWS_
typedef	unsigned int		BOOL;
#define	TRUE				1
#define	FALSE				0
#endif
#ifndef	_WINDOWS_
typedef	unsigned int		UINT;
typedef	unsigned int		UINT32;
typedef	unsigned int		DWORD;
typedef	signed int			INT;
typedef	signed int			INT32;
typedef	int					UINT_PTR;
typedef	long				LONG_PTR;
#endif
typedef	unsigned short		WORD;
typedef	unsigned short		USHORT;
typedef	signed short		SHORT;
typedef	unsigned char		BYTE;
typedef	unsigned char		UCHAR;
typedef signed char			CHAR;
typedef	unsigned long long	UINT64;
typedef signed long long	INT64;

#include "common.h"

#define HEADER_ONLY
#include "GeneratedCode.c"

#ifdef _WIN32
UINT64 MsGetHiResTimeSpan(UINT64 diff)
{
	LARGE_INTEGER t; UINT64 freq;
	if (QueryPerformanceFrequency(&t) == false) freq = 1000ULL;
	else memcpy(&freq, &t, sizeof(UINT64));
	return (UINT64)diff * 1000000000UL / (UINT64)freq;
}
UINT64 MsGetHiResCounter()
{
	LARGE_INTEGER t; UINT64 ret;
	if (QueryPerformanceCounter(&t) == false) return 0xffffffffffffffffull;
	memcpy(&ret, &t, sizeof(UINT64));
	return ret;
}
UINT64 TickHighres64()
{
	return (UINT64)(MsGetHiResTimeSpan(MsGetHiResCounter()));
}
UINT64 Tick64()
{
	return TickHighres64() / 1000000ull;
}
#else
UINT64 Tick64()
{
	struct timespec t = { 0 };
	clock_gettime(CLOCK_MONOTONIC, &t);
	return ((UINT64)((UINT32)t.tv_sec)) * 1000LL + (UINT64)t.tv_nsec / 1000000LL;
}
#endif
void ToStr64(char *str, UINT64 value)
{
	char tmp[256];
	UINT wp = 0;
	UINT len, i;
	strcpy(tmp, "");
	// Append from the last digit
	while (true)
	{
		UINT a = (UINT)(value % (UINT64)10);
		value = value / (UINT64)10;
		tmp[wp++] = (char)('0' + a);
		if (value == 0)
		{
			tmp[wp++] = 0;
			break;
		}
	}
	len = strlen(tmp);
	for (i = 0;i < len;i++)
	{
		str[len - i - 1] = tmp[i];
	}
	str[len] = 0;
}
void ToStr3(char *str, UINT64 v)
{
	char tmp[128];
	char tmp2[128];
	UINT i, len, wp;
	ToStr64(tmp, v);
	wp = 0;
	len = strlen(tmp);
	for (i = len - 1;((int)i) >= 0;i--)
	{
		tmp2[wp++] = tmp[i];
	}
	tmp2[wp++] = 0;
	wp = 0;
	for (i = 0;i < len;i++)
	{
		if (i != 0 && (i % 3) == 0)
		{
			tmp[wp++] = ',';
		}
		tmp[wp++] = tmp2[i];
	}
	tmp[wp++] = 0;
	wp = 0;
	len = strlen(tmp);

	for (i = len - 1;((int)i) >= 0;i--)
	{
		tmp2[wp++] = tmp[i];
	}
	tmp2[wp++] = 0;

	strcpy(str, tmp2);
}


void mem_write(VPageTableEntry *pte, uint address, uint value)
{
	uint vaddr = address;
	uint vaddr1_index = vaddr / 4096;
	uint vaddr1_offset = vaddr % 4096;
	if (pte[vaddr1_index].CanWrite == false)
	{
		printf("Access violation to 0x%x.", vaddr);
		exit(0);
	}
	*((uint*)((byte*)(pte[vaddr1_index].RealMemory + vaddr1_offset))) = value;
}

uint mem_read(VPageTableEntry *pte, uint address)
{
	uint vaddr = address;
	uint vaddr1_index = vaddr / 4096;
	uint vaddr1_offset = vaddr % 4096;
	if (pte[vaddr1_index].CanWrite == false)
	{
		printf("Access violation to 0x%x.", vaddr);
		exit(0);
	}
	return *((uint*)((byte*)(pte[vaddr1_index].RealMemory + vaddr1_offset)));
}



void AllignMemoryToPage(uint startAddress, uint size, uint *pageNumberStart, uint *pageCount)
{
	*pageNumberStart = startAddress / 4096;

	*pageCount = (startAddress + size) / 4096 - *pageNumberStart;
}

void AllocateMemory(VMemory *memory, uint startAddress, uint size, bool canRead, bool canWrite)
{
	uint pageNumberStart, pageCount;
	AllignMemoryToPage(startAddress, size, &pageNumberStart, &pageCount);
	for (uint i = pageNumberStart; i < pageNumberStart + pageCount; i++)
	{
		VPageTableEntry* e = &memory->PageTableEntry[i];
		if (e->RealMemory != null)
		{
			printf("Page number %u, count %u already exists.\n", pageNumberStart, pageCount);
		}
	}
	for (uint i = pageNumberStart; i < pageNumberStart + pageCount; i++)
	{
		VPageTableEntry* e = &memory->PageTableEntry[i];
		e->RealMemory = (byte*)malloc((int)4096);
		e->CanRead = canRead;
		e->CanWrite = canWrite;
	}
}

MS_ABI NOINLINE UINT64 test_get_fs_register();
MS_ABI NOINLINE UINT64 test_get_gs_register();

MS_ABI NOINLINE UINT64 asm_read_memory_with_fs(UINT64 addr);
MS_ABI NOINLINE UINT64 asm_read_memory_with_gs(UINT64 addr);

MS_ABI NOINLINE void asm_write_memory_with_fs(UINT64 addr, UINT64 value);
MS_ABI NOINLINE void asm_write_memory_with_gs(UINT64 addr, UINT64 value);

MS_ABI NOINLINE void test_set_fs_register(UINT64 v);
MS_ABI NOINLINE void test_set_gs_register(UINT64 v);

MS_ABI NOINLINE void c2asm_func2(C2ASM *t);

MS_ABI NOINLINE void c2asm_func1(C2ASM *t)
{
	t->c = t->a * 0x28B8888 /* = 42698888 */ + t->b * 0x4E20000 /* = 81920000 */;
}

MS_ABI NOINLINE void c2asm_test1()
{
	C2ASM t;
	
	memset(&t, 0, sizeof(t));
	
	t.a = 0x351111  /* = 3477777 */;
	t.b = 0x2567777 /* = 39221111 */;
	
	t.f = 4;
	c2asm_func2(&t);
	 
	printf("c = %X\n", t.c);
	// c = 68B1908

	printf("d = %X\n", t.d);
}

#ifdef _WIN32
#define USE_ASM_FS_GS_GET_SET
#endif // _WIN32


MS_ABI NOINLINE UINT64 syscall_get_fs_register()
{
#ifdef USE_ASM_FS_GS_GET_SET
	return test_get_fs_register();
#else
	UINT64 ret = 0;
	if (arch_prctl(ARCH_GET_FS, &ret) == -1)
	{
		printf("arch_prctl ARCH_GET_FS error: %u\n", errno);
		return 0;
	}
	return ret;
#endif
}

MS_ABI NOINLINE UINT64 syscall_get_gs_register()
{
#ifdef USE_ASM_FS_GS_GET_SET
	return test_get_gs_register();
#else
	UINT64 ret = 0;
	if (arch_prctl(ARCH_GET_GS, &ret) == -1)
	{
		printf("arch_prctl ARCH_GET_GS error: %u\n", errno);
		return 0;
	}
	return ret;
#endif
}

MS_ABI NOINLINE void syscall_set_fs_register(UINT64 v)
{
#ifdef USE_ASM_FS_GS_GET_SET
	test_set_fs_register(v);
#else
	if (arch_prctl(ARCH_SET_FS, (unsigned long *)v) == -1)
	{
		printf("arch_prctl ARCH_SET_FS error: %u\n", errno);
	}
#endif
}

MS_ABI NOINLINE void syscall_set_gs_register(UINT64 v)
{
#ifdef USE_ASM_FS_GS_GET_SET
	test_set_gs_register(v);
#else
	if (arch_prctl(ARCH_SET_GS, (unsigned long *)v) == -1)
	{
		printf("arch_prctl ARCH_SET_GS error: %u\n", errno);
	}
#endif
}


void raw_stdout_write(char *str)
{
#ifdef _WIN32
	write(1, str, strlen(str));
#else
	write(1, str, strlen(str));
#endif // !_WIN32
}

void print_uint64(char *prefix, UINT64 v)
{
	char tmp[100];
	char tmp2[512];
	ToStr64(tmp, v);
	strcpy(tmp2, prefix);
	strcat(tmp2, tmp);
	strcat(tmp2, "\n");

	raw_stdout_write(tmp2);

#ifndef _WIN32
	//fsync(1);

#endif // !_WIN32
}

void fs_gs_test()
{
	UINT64 fs1 = 12345678;
	UINT64 gs1 = 87654321;

	UINT64 test_array[] =
	{
		0,
		55555555,
		0,
		66666666,
		0,
		77777777,
		0,
		88888888,
		0,
	};

	UINT64 fs2 = 0;
	UINT64 gs2 = 0;

	print_uint64("test", 123);

	fs2 = syscall_get_fs_register();
	gs2 = syscall_get_gs_register();
	print_uint64("original fs: ", fs2);
	print_uint64("original gs: ", gs2);
	raw_stdout_write("\n");

	fs1 = (UINT64)(&test_array) + 8;
	gs1 = (UINT64)(&test_array) + 24;

	// Memo: Windows x64 の GS レジスタについて
	//       あるコンテキストスイッチの内部で GS レジスタを触った場合は必ず Win32 API を呼び出す前に 0 に戻す必要がある。
	//       また、コンテキストスイッチが発生した後に戻ってきたら GS レジスタは Windows 内部情報 (x64 / x86 変換レイヤ?) の
	//       値が勝手に書き込まれてしまっている。
	// 
	// Memo: Windows x64 の FS レジスタについて
	//       また、コンテキストスイッチが発生した後に戻ってきたら FS レジスタは Windows 内部情報 (x64 / x86 変換レイヤ?) の
	//       値が勝手に 0 になってしまっている。
	//
	//       WSL (Windows Subsystem for Linux) では FS, GS レジスタの値はちゃんと保存されているので、一体何が違うのか、
	//       WSL のコードに対して詰問が必要だ。
	//       まったく、けしからん。

	syscall_set_fs_register(fs1);
	syscall_set_gs_register(gs1);

	while (true)
	{
		UINT64 read_fs = 0, read_gs = 0;

		fs2 = syscall_get_fs_register();
		gs2 = syscall_get_gs_register();

		read_fs = asm_read_memory_with_fs(0);
		read_gs = asm_read_memory_with_gs(0);
		
		//syscall_set_gs_register(0);

		print_uint64("fs: ", fs2);
		print_uint64("gs: ", gs2);
		print_uint64("read_fs: ", read_fs);
		print_uint64("read_gs: ", read_gs);

		raw_stdout_write("\n");
	}
}

int main()
{
	fs_gs_test(); return;

	//c2asm_test1(); return;

	uint count = 10;
	uint stackPtr = 0x500000 + 0x10000 / 2;
	ulong size;

	VMemory *memory = malloc(sizeof(VMemory));
   
    size = (int)(sizeof(VPageTableEntry) * (uint)(0x100000000 / 4096));
	memory->PageTableEntry = malloc(size);
	for (uint i = 0; i < (uint)(0x100000000 / 4096); i++)
	{
		memory->PageTableEntry[i].RealMemory = null;
		memory->PageTableEntry[i].CanRead = memory->PageTableEntry[i].CanWrite = false;
	}

	AllocateMemory(memory, 0x8000000, 0x100000, true, true);
	AllocateMemory(memory, 0x500000, 0x10000, true, true);

	uint cont_size = 0x200000;
	memory->ContiguousMemory = malloc(cont_size);
	memory->ContiguousStart = 0x500000;
	memory->ContiguousEnd = memory->ContiguousStart + cont_size;

	VCpuState *state = malloc(sizeof(VCpuState));
	memset(state, 0, sizeof(VCpuState));

	state->Memory = memory;

	uint ret = 0xffffffff;
	
	ulong tick_start = Tick64();
	
	// SWITCH!!!
	state->UseAsm = true;

	if (state->UseAsm)
	{
		memory->ContiguousMemory = AsmContMemory;
	}

	// TODO: ここで問題発生; 取得されるアドレスが不正 リロケーションうまくいってない?
	printf("ASN_GLOBAL_CONT_MEM = 0x%p\n", memory->ContiguousMemory);
	printf("ASM_GLOBAL_CONT_MEM_MINUS_START = 0x%p\n", ASM_GLOBAL_CONT_MEM_MINUS_START);
	printf("dynasm = 0x%p\n", dynasm);

	for (uint i = 0;i < count;i++)
	{
		state->Esp = stackPtr;
		state->Esp -= 4;
		
		if (state->UseAsm == false)
		{
			mem_write(memory->PageTableEntry, state->Esp, CallRetAddress__MagicReturn);
			*((uint*)(byte*)(memory->ContiguousMemory + state->Esp - memory->ContiguousStart)) = CallRetAddress__MagicReturn;
		}
		else
		{
			mem_write(memory->PageTableEntry, state->Esp, 0x7fffffff);
			*((uint*)(byte*)(memory->ContiguousMemory + state->Esp - memory->ContiguousStart)) = 0x7fffffff;
		}

		Iam_The_IntelCPU_HaHaHa(state, FunctionTable_test_target3);

		if (state->ExceptionString[0] != 0)
		{
			printf("Error: %s at 0x%x.\n", state->ExceptionString, state->ExceptionAddress);
			exit(0);
		}
		else
		{
			uint r = state->Eax;

			if (ret == 0xffffffff)
			{
				ret = r;
				printf("ret = %u\n", state->Eax);
			}
			else if (ret == r) {}
			else
			{
				printf("Error: Invalid result: %u\n", r);
				exit(0);
			}
		}
	}

	ulong tick_end = Tick64();

	printf("time = %u\n", (UINT)((tick_end - tick_start) / count));

	return 0;
}


