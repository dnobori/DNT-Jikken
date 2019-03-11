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


#ifdef _WIN32
#define NOINLINE	__declspec(noinline)
#define MS_ABI
#define PACKED
#else
#define NOINLINE	__attribute__((noinline))
#define MS_ABI		__attribute__((ms_abi))
#define	PACKED		__attribute__ ((__packed__))
#endif


MS_ABI NOINLINE UINT asm_sum(UINT a, UINT b);

MS_ABI NOINLINE UINT64 asm_test1();
