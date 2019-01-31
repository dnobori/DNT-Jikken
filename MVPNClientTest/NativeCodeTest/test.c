#include <stdio.h>

#ifdef _WIN32
#include <windows.h>
#define NOINLINE	__declspec(noinline)
#else
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


UINT j;
UINT total = 0;

NOINLINE UINT test_target1()
{

	for (j = 3;j <= 20000;j++)
	{
		UINT k;
		bool ok = true;

		for (k = 2;k < j;k++)
		{
			if ((j % k) == 0)
			{
				ok = false;
				break;
			}
		}

		if (ok)
		{
			total++;
		}
	}

	return total; // 2261
}




int main()
{
	printf("%u\n", test_target1());
	return 0;
}


