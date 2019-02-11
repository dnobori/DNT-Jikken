#pragma  once

#ifdef _WIN32
#else
#define likely(x)   __builtin_expect(!!(x), 1)
#define unlikely(x) __builtin_expect(!!(x), 0)
#endif

typedef	unsigned short		ushort;

typedef	unsigned int		uint;
typedef	unsigned long long	ulong;
typedef	unsigned char		byte;

typedef	unsigned int		bool;
#define	true				1
#define	false				0

#define	null ((void *)0)

#ifdef GENERATED_CODE_C
int  sprintf(
	char*       const _Buffer,
	char const* const _Format,
	...);

#define	true				1
#define	false				0

#endif // GENERATED_CODE_C


typedef struct VPageTableEntry
{
	byte* RealMemory;
	bool CanRead;
	bool CanWrite;
} VPageTableEntry;

typedef struct VMemory
{
	volatile VPageTableEntry *PageTableEntry;
	volatile byte *ContiguousMemory;
	volatile uint ContiguousStart;
	volatile uint ContiguousEnd;
} VMemory;

typedef struct VCpuState
{
	volatile VMemory *Memory;
	volatile uint Eax, Ebx, Ecx, Edx, Esi, Edi, Ebp, Esp;
	volatile char ExceptionString[256];
	volatile uint ExceptionAddress;
	volatile bool Interrupt;
} VCpuState;

void Iam_The_IntelCPU_HaHaHa(VCpuState *state, uint ip);

