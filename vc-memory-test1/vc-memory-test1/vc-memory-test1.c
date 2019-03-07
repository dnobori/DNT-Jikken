#define _CRT_SECURE_NO_WARNINGS
#include <windows.h>
#include <stdio.h>

void mmap_test4()
{
	HANDLE h = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE | SEC_RESERVE, 1, 0, NULL);
	if (h == NULL)
	{
		printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
	}
	UINT64 a;
	SYSTEM_INFO info;
	ZeroMemory(&info, sizeof(info));
	GetSystemInfo(&info);
	printf("alloc start...\n");
	UINT alloc_uint = 65536;
	for (a = 0;a < 0x100000000ULL; a += alloc_uint)
	{
		UCHAR *p1 = MapViewOfFileEx(h, FILE_MAP_WRITE, 0, (DWORD)a, alloc_uint, NULL);
		if (p1 == NULL)
		{
			printf("CreateFileMapping fail. a = %u, error: %u\n", (UINT)a, GetLastError()); Sleep(INFINITE);
		}
	}
	printf("alloc end.\n");
}

void mmap_test5()
{
	int i;
	for (i = 0;i < 10000;i++)
	{
		mmap_test4();
		printf("%u\n", i);
	}
}

void mmap_test3()
{
	int i;
	HANDLE h = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE | SEC_RESERVE, 1, 0, NULL);
	if (h == NULL)
	{
		printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
	}
	UCHAR *p1 = MapViewOfFileEx(h, FILE_MAP_WRITE, 0, 0, 0, NULL);
	if (p1 == NULL)
	{
		printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
	}
	//printf("p1 = 0x%p\n", p1);

	if (VirtualAlloc(p1, 0x100000000ULL, MEM_COMMIT, PAGE_READWRITE) == NULL)
	{
		printf("VirtualAlloc fail. error: %u\n", GetLastError()); Sleep(INFINITE);
	}
	strcpy(p1, "Hello ");
	for (i = 0;i < 1000;i++)
	{
		UCHAR *p2 = MapViewOfFileEx(h, FILE_MAP_COPY | FILE_MAP_WRITE, 0, 0, 0, NULL);
		if (p2 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		UCHAR *p3 = MapViewOfFileEx(h, FILE_MAP_COPY | FILE_MAP_WRITE, 0, 0, 0, NULL);
		if (p3 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}

		//printf("p2 = 0x%p\n", p2);
		//printf("p3 = 0x%p\n", p3);
		//printf("\n");

		//UnmapViewOfFile(p1);

		//printf("p2_str: %s\n", p2);
		//printf("p3_str: %s\n", p3);
		//printf("\n");

		strcpy(p2 + 6, "Neko");
		strcpy(p3 + 6, "World");

		//printf("p2_str: %s\n", p2);
		//printf("p3_str: %s\n", p3);

		//UnmapViewOfFile(p2);
		//UnmapViewOfFile(p3);


		printf("%u\n", i);

		//break;
	}
}

void mmap_test2()
{
	int i;
	for (i = 0;i < 10000;i++)
	{
		HANDLE h = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE | SEC_RESERVE, 1, 0, NULL);
		if (h == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}

		UCHAR *p1 = MapViewOfFileEx(h, FILE_MAP_WRITE, 0, 0, 0, NULL);
		if (p1 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		printf("p1 = 0x%p\n", p1);

		if (VirtualAlloc(p1, 4096, MEM_COMMIT, PAGE_READWRITE) == NULL)
		{
			printf("VirtualAlloc fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		strcpy(p1, "Hello ");

		UCHAR *p2 = MapViewOfFileEx(h, FILE_MAP_COPY, 0, 0, 0, NULL);
		if (p2 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		UCHAR *p3 = MapViewOfFileEx(h, FILE_MAP_COPY, 0, 0, 0, NULL);
		if (p3 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		
		printf("p2 = 0x%p\n", p2);
		printf("p3 = 0x%p\n", p3);
		printf("\n");

		UnmapViewOfFile(p1);

		printf("p2_str: %s\n", p2);
		printf("p3_str: %s\n", p3);
		printf("\n");

		strcpy(p2 + 6, "Neko");
		strcpy(p3 + 6, "World");

		printf("p2_str: %s\n", p2);
		printf("p3_str: %s\n", p3);

		UnmapViewOfFile(p2);
		UnmapViewOfFile(p3);

		CloseHandle(h);

		printf("%u\n", i);

		break;
	}
}


void mmap_test2_1()
{
	UINT64 vsize = 0x100000000ULL;
	int i;
	HANDLE h = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE | SEC_RESERVE, 1, 0, NULL);
	if (h == NULL)
	{
		printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
	}

	UCHAR *p1 = MapViewOfFileEx(h, FILE_MAP_WRITE, 0, 0, 0, NULL);
	if (p1 == NULL)
	{
		printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
	}
	printf("p1 = 0x%p\n", p1);

	if (VirtualAlloc(p1, vsize, MEM_COMMIT, PAGE_READWRITE) == NULL)
	{
		printf("VirtualAlloc fail. error: %u\n", GetLastError()); Sleep(INFINITE);
	}
	strcpy(p1, "Hello ");
	DWORD old = 0;
	VirtualProtect(p1, vsize, PAGE_WRITECOPY, &old);
	for (i = 0;i < 10000;i++)
	{

		UCHAR *p2 = MapViewOfFileEx(h, FILE_MAP_COPY, 0, 0, 0, NULL);
		if (p2 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		UCHAR *p3 = MapViewOfFileEx(h, FILE_MAP_COPY, 0, 0, 0, NULL);
		if (p3 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}

		printf("p1 = 0x%p\n", p1);
		printf("p2 = 0x%p\n", p2);
		printf("p3 = 0x%p\n", p3);
		printf("\n");

		printf("p1_str: %s\n", p1);
		printf("p2_str: %s\n", p2);
		printf("p3_str: %s\n", p3);
		printf("\n");

		strcpy(p1 + 6, "Zuru");
		strcpy(p2 + 6, "Neko");
		strcpy(p3 + 6, "World");

		printf("p1_str: %s\n", p1);
		printf("p2_str: %s\n", p2);
		printf("p3_str: %s\n", p3);

		//UnmapViewOfFile(p1);
		UnmapViewOfFile(p2);
		UnmapViewOfFile(p3);

		//CloseHandle(h);

		printf("%u\n", i);

		//break;
	}
}


void mmap_test2_0()
{
	UINT64 vsize = 0x100000000ULL;
	int i;
	for (i = 0;i < 10000;i++)
	{
		HANDLE h = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE | SEC_RESERVE, 1, 0, NULL);
		if (h == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}

		UCHAR *p1 = MapViewOfFileEx(h, FILE_MAP_WRITE, 0, 0, 0, NULL);
		if (p1 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		printf("p1 = 0x%p\n", p1);

		if (VirtualAlloc(p1, vsize, MEM_COMMIT, PAGE_READWRITE) == NULL)
		{
			printf("VirtualAlloc fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		DWORD old = 0;

//		VirtualProtect(p1, vsize, PAGE_WRITECOPY, &old);
		strcpy(p1, "Hello ");
		VirtualProtect(p1, vsize, PAGE_READWRITE, &old);
		strcpy(p1, "Hello2 ");

		UCHAR *p2 = MapViewOfFileEx(h, FILE_MAP_COPY, 0, 0, 0, NULL);
		if (p2 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		UCHAR *p3 = MapViewOfFileEx(h, FILE_MAP_COPY, 0, 0, 0, NULL);
		if (p3 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}

		printf("p1 = 0x%p\n", p1);
		printf("p2 = 0x%p\n", p2);
		printf("p3 = 0x%p\n", p3);
		printf("\n");

		printf("p1_str: %s\n", p1);
		printf("p2_str: %s\n", p2);
		printf("p3_str: %s\n", p3);
		printf("\n");

		strcat(p1, " Zuru");
		strcat(p2, " Neko");
		strcat(p3, " World");

		printf("p1_str: %s\n", p1);
		printf("p2_str: %s\n", p2);
		printf("p3_str: %s\n", p3);

		UnmapViewOfFile(p1);
		UnmapViewOfFile(p2);
		UnmapViewOfFile(p3);

		CloseHandle(h);

		printf("%u\n", i);

		break;
	}
}


void mmap_test2_2()
{
	UINT64 vsize = 0x100000000ULL;
	int i;
	for (i = 0;i < 10000;i++)
	{
		HANDLE h = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE | SEC_RESERVE, 1, 0, L"map1");
		if (h == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}

		UCHAR *p1 = MapViewOfFileEx(h, FILE_MAP_WRITE, 0, 0, 0, NULL);
		if (p1 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		printf("p1 = 0x%p\n", p1);

		if (VirtualAlloc(p1, vsize, MEM_COMMIT, PAGE_READWRITE) == NULL)
		{
			printf("VirtualAlloc fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		DWORD old = 0;

		//		VirtualProtect(p1, vsize, PAGE_WRITECOPY, &old);
		strcpy(p1, "Hello ");

		HANDLE h2 = OpenFileMapping(FILE_MAP_COPY, FALSE, L"map1");
		if (h2 == NULL)
		{
			printf("OpenFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		UCHAR *p2 = MapViewOfFileEx(h2, FILE_MAP_COPY, 0, 0, 0, NULL);
		if (p2 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}
		UCHAR *p3 = MapViewOfFileEx(h, FILE_MAP_COPY, 0, 0, 0, NULL);
		if (p3 == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError()); Sleep(INFINITE);
		}

		printf("p1 = 0x%p\n", p1);
		printf("p2 = 0x%p\n", p2);
		printf("p3 = 0x%p\n", p3);
		printf("\n");

		printf("p1_str: %s\n", p1);
		printf("p2_str: %s\n", p2);
		printf("p3_str: %s\n", p3);
		printf("\n");

		strcat(p1, " Zuru");
		strcat(p2, " Neko");
		strcat(p3, " World");

		printf("p1_str: %s\n", p1);
		printf("p2_str: %s\n", p2);
		printf("p3_str: %s\n", p3);

		UnmapViewOfFile(p1);
		UnmapViewOfFile(p2);
		UnmapViewOfFile(p3);

		CloseHandle(h);

		printf("%u\n", i);

		break;
	}
}


void mmap_test1()
{
	int i;
	for (i = 0;i < 10;i++)
	{
		//void *p = VirtualAlloc(NULL, 0x100000000ULL, MEM_RESERVE, PAGE_READWRITE);
		HANDLE h = CreateFileMapping(NULL, NULL, PAGE_READWRITE | SEC_RESERVE, 1, 0, NULL);
		if (h == NULL)
		{
			printf("CreateFileMapping fail. error: %u\n", GetLastError());
			Sleep(INFINITE);
		}

		void *p = MapViewOfFileEx(h, FILE_MAP_WRITE, 0, 0, 0, NULL);

		if (p == NULL)
		{
			printf("MapViewOfFileEx fail. error: %u\n", GetLastError());
			Sleep(INFINITE);
		}

		//if (VirtualAlloc(p, 4096 * 8, MEM_COMMIT, PAGE_READWRITE) == FALSE)
		//{
		//	printf("VirtualAlloc fail. error: %u\n", GetLastError());
		//	Sleep(INFINITE);
		//}
		////VirtualFree((void *)((UINT64)p + 4096 * 0), 4096, MEM_DECOMMIT);
		//UINT *a = (UINT *)p;
		//*a = 123;

		UINT x;
		for (x = 0;x < (UINT)((0x100000000ULL) / 4096ULL); x++)
		{
			void *pp;
			if ((pp = VirtualAlloc((void *)((UINT64)p + 4096 * x), 4096, MEM_COMMIT, PAGE_READWRITE)) == NULL)
			{
				printf("VirtualAlloc fail. error: %u\n", GetLastError());
				Sleep(INFINITE);
			}
			else
			{
				//printf("pp = %p  %p\n", pp, (void *)((UINT64)p + 4096 * x));
			}
		}
		printf("%u: %p\n", i, p);
	}
	return;
}

int main()
{
	//mmap_test1();
	//mmap_test2();
	//mmap_test2_0();
	mmap_test5();
	//mmap_test3();
	//mmap_test4();

	printf("ok.\n");
	Sleep(INFINITE);

	return 0;
}

