#include <stdio.h>

#ifdef _WIN32
#include <windows.h>
#define NOINLINE	__declspec(noinline)
#else
#include <time.h>
#define NOINLINE	__attribute__((noinline))
#endif

#ifndef	_WINDOWS_
typedef	unsigned int		BOOL;
#define	TRUE				1
#define	FALSE				0
#endif
typedef	unsigned int		bool;
#define	true				1
#define	false				0
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



int main()
{
	uint count = 5;
	uint stackPtr = 0x500000 + 0x10000 / 2;

	VMemory *memory = malloc(sizeof(VMemory));

	memory->ContiguousMemory = malloc(0x8000000 + 0x100000 - 0x500000);
	memory->ContiguousStart = 0x500000;
	memory->ContiguousEnd = memory->ContiguousStart + 0x8000000 + 0x100000 - 0x500000;

	VCpuState *state = malloc(sizeof(VCpuState));
	memset(state, 0, sizeof(VCpuState));

	state->Memory = memory;

	uint ret = 0xffffffff;

	ulong tick_start = Tick64();

	for (uint i = 0;i < count;i++)
	{
		state->Esp = stackPtr;
		state->Esp -= 4;

		*((uint*)(byte*)(memory->ContiguousMemory + state->Esp - memory->ContiguousStart)) = CallRetAddress__MagicReturn;

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


