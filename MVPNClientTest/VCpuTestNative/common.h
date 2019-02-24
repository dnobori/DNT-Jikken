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

#ifdef _WIN32
#define NOINLINE	__declspec(noinline)
#define MS_ABI
#define PACKED
#else
#define NOINLINE	__attribute__((noinline))
#define MS_ABI		__attribute__((ms_abi))
#define	PACKED		__attribute__ ((__packed__))
#endif


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

	volatile bool UseAsm;
} VCpuState;

void Iam_The_IntelCPU_HaHaHa(VCpuState *state, uint ip);

typedef struct C2ASM
{
	uint a;
	uint b;
	uint c;
	uint d, e, f;
} C2ASM;


#ifdef	_WIN32
#pragma pack(push, 1)
#endif	// _WIN32

typedef struct DYNASM_CPU_STATE
{
	volatile byte *ContMemMinusStart;
	volatile uint Eax, Ebx, Ecx, Edx, Esi, Edi, Ebp, Esp;

	volatile uint StartIp;
	volatile uint ExceptionAddress;
	volatile uint ExceptionType;
} DYNASM_CPU_STATE PACKED;

MS_ABI NOINLINE void dynasm(DYNASM_CPU_STATE *t);

#ifdef	_WIN32
#pragma pack(pop)
#endif	// _WIN32


