#include "test.h"

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


NOINLINE UINT dummy1(UINT a, UINT b)
{
	return a + b;
}

NOINLINE UINT dummy2(UINT a, UINT b, UINT c, UINT d, UINT e, UINT f, UINT g)
{
	return a + b + c + d + e + f + g;
}

int main()
{
	volatile UINT r = 0;
	r = dummy1(1, 2);
	r = dummy2(1, 2, 3, 4, 5, 6, 7);
	r = asm_sum(1, 2);
    

	volatile UINT64 r64 = 0;

	r64 = asm_test1(0x80000000, 0xF0000000);
	printf("%p\n", (void *)r64);

	return 0;
}


