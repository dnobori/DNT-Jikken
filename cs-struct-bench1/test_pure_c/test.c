typedef	unsigned int		BOOL;
#define	TRUE				1
#define	FALSE				0
typedef	unsigned int		bool;
#define	true				1
#define	false				0
typedef	unsigned int		UINT;
typedef	unsigned int		UINT32;
typedef	unsigned int		DWORD;
typedef	signed int			INT;
typedef	signed int			INT32;
typedef	int					UINT_PTR;
typedef	long				LONG_PTR;
typedef	unsigned short		WORD;
typedef	unsigned short		USHORT;
typedef	signed short		SHORT;
typedef	unsigned char		BYTE;
typedef	unsigned char		UCHAR;
typedef signed char			CHAR;
typedef	unsigned long long	UINT64;
typedef signed long long	INT64;

#define NOINLINE	__attribute__((noinline))

NOINLINE UINT fib(UINT a);

NOINLINE UINT pure_c_test1()
{
	volatile UINT a = 0;

	return fib(a);

	//ret = 5702887
	//time = 36,055,870
}

NOINLINE UINT fib(UINT a)
{
	if (a == 0)
	{
		return 0;
	}
	else if (a == 1)
	{
		return 1;
	}
	else
	{
		return fib(a - 1) + fib(a - 2);
	}
}

int main()
{
    pure_c_test1();
    return 0;
}

