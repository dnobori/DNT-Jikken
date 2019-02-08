#pragma  once

typedef	unsigned short		ushort;

typedef	unsigned int		uint;
typedef	unsigned long long	ulong;
typedef	unsigned char		byte;


#define	null ((void *)0)

#ifdef GENERATED_CODE_C
int __cdecl sprintf(
	char*       const _Buffer,
	char const* const _Format,
	...);

#define	true				1
#define	false				0

#endif // GENERATED_CODE_C


typedef struct VPageTableEntry
{
	int a;
} VPageTableEntry;

typedef struct VMemory
{
	VPageTableEntry *PageTableEntry;
	byte *ContiguousMemory;
	uint ContiguousStart;
	uint ContiguousEnd;
} VMemory;

typedef struct VCpuState
{
	VMemory *Memory;
	uint Eax, Ebx, Ecx, Edx, Esi, Edi, Ebp, Esp;
	char ExceptionString[256];
	uint ExceptionAddress;
} VCpuState;

void Iam_The_IntelCPU_HaHaHa(VCpuState *state, uint ip);

