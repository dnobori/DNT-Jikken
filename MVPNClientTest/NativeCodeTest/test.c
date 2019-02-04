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
#else
UINT64 TickHighres64() {
	return 0;
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


NOINLINE UINT test_target1()
{
	UINT j;
	UINT total = 0;
	volatile UINT p = 20000;

	for (j = 3;j <= p;j++)
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

	return total;

	//ret = 2261
	//time = 51,014,080
}
//
//NOINLINE UINT test_target2()
//{
//	UINT tmp[2000];
//	volatile UINT p = sizeof(tmp) / sizeof(tmp[0]);
//	UINT i, j;
//	UINT ret = 0;
//
//	for (i = 0;i < p;i++)
//	{
//		tmp[i] = i;
//	}
//	for (j = 0;j < 50000;j++)
//	{
//		for (i = 0;i < p;i++)
//		{
//			ret += tmp[i];
//		}
//	}
//	return ret;
//
//	//ret = 1165752192
//	//time = 60,848,300
//}
//
//NOINLINE UINT test_target4(UINT a)
//{
//	if (a == 0)
//	{
//		return 0;
//	}
//	else if (a == 1)
//	{
//		return 1;
//	}
//	else
//	{
//		return test_target4(a - 1) + test_target4(a - 2);
//	}
//}
//
//NOINLINE UINT test_target3()
//{
//	volatile UINT a = 34;
//
//	return test_target4(a);
//
//	//ret = 5702887
//	//time = 36,055,870
//}
//

//// Tak
//NOINLINE int test_target6(int x, int y, int z) {
//	if (x <= y) {
//		return z;
//	}
//	else {
//		volatile UINT a = 0;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++; while (a && test_target6(1, 2, 3)) a++;
//		return test_target6(test_target6(x - 1, y, z), test_target6(y - 1, z, x), test_target6(z - 1, x, y));
//	}
//}
//
//NOINLINE UINT test_target5()
//{
//	volatile UINT x = 25, y = 7, z = 0;
//	UINT ret = test_target6(x, y, z);
//	return ret;
//	//ret = 1
//	//time = 32,209,370
//}

NOINLINE UINT test_main(UINT count)
{
	UINT ret = 0xffffffff;
	UINT i;

	for (i = 0;i < count;i++)
	{
		UINT r = test_target1();
		//UINT r = test_target2();
		//UINT r = test_target3();
		//UINT r = test_target5();

		if (ret == 0xffffffff || ret == r)
		{
			ret = r;
		}
		else
		{
			return 0xffffffff;
		}
	}

	return ret;
}



int main()
{
	UINT64 start;
	UINT64 end;
	char tmp[256];
	UINT count = 10;
	UINT64 result;

	UINT ret;

	start = TickHighres64();
	
	ret = test_main(count);

	end = TickHighres64();

	result = (end - start) / (UINT64)count;

	printf("ret = %u\n", ret);

	
	ToStr3(tmp, result);
	printf("time = %s\n", tmp);

	return 0;
}


