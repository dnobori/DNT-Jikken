#include <windows.h>
#include <stdio.h>



int main()
{
    int i;
    for (i = 0;i<100;i++)
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
			if (VirtualAlloc((void *)((UINT64)p + 4096 * x), 4096, MEM_COMMIT, PAGE_READWRITE) == FALSE)
			{
				printf("VirtualAlloc fail. error: %u\n", GetLastError());
				Sleep(INFINITE);
			}
		}
		printf("%u: %p\n", i, p);
	}
    Sleep(INFINITE);
    return 0;
}

