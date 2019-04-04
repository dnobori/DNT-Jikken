// Temporary source file
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>
#include <stdarg.h>
#include <locale.h>
#include <time.h>
#include <errno.h>

#include <nativelib.h>
#include "backup-test1.h"

void Temp_TestFunction(char *tmp)
{
	if (true)
	{
		if (MsEnablePrivilege("SeBackupPrivilege", true) == false)
		{
			Print("MsEnablePrivilege error.\n");
			return;
		}

		if (MsEnablePrivilege("SeRestorePrivilege", true) == false)
		{
			Print("MsEnablePrivilege error.\n");
			return;
		}
	}

	HANDLE h = CreateFileA("D:\\tmp\\dirtest\\1\\1.txt", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, NULL);
	if (h == INVALID_HANDLE_VALUE)
	{
		Print("CreateFile error: %u\n", GetLastError());
	}
	else
	{
		DWORD size = GetFileSize(h, NULL);
		Print("File size: %u\n", size);

		CloseHandle(h);
	}

	DIRLIST *d = EnumDir("D:\\tmp\\dirtest\\1");
	if (d == NULL)
	{
		Print("EnumDir error: %u\n", GetLastError());
	}
	else
	{
		Print("Dir list:\n");
		for (UINT i = 0;i < d->NumFiles;i++)
		{
			Print("%s\n", d->File[i]->FileName);
		}

		FreeDir(d);
	}

	if (CreateDirectoryA("D:\\tmp\\dirtest\\1\\new_dir_2", NULL) == false)
	{
		Print("CreateDirectoryA error: %u\n", GetLastError());
	}
}


