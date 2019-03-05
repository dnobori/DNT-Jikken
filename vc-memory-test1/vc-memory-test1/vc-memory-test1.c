#include <windows.h>
#include <stdio.h>



int main()
{
    int i;
    for (i = 0;;i++)
    {
        void *p = VirtualAlloc(NULL, 0x100000000ULL, MEM_RESERVE, PAGE_READWRITE);
        if (p == NULL) Sleep(INFINITE);
        printf("%p\n", p);
    }
    //Sleep(INFINITE);
    return 0;
}

