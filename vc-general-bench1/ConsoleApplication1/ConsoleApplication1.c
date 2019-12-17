// ConsoleApplication1.cpp : このファイルには 'main' 関数が含まれています。プログラム実行の開始と終了がそこで行われます。
//

#include <stdio.h>
#include <stdlib.h>
#include <windows.h>

// Get the precise time from the value of the high-resolution counter
double MsGetHiResTimeSpan(UINT64 diff)
{
	LARGE_INTEGER t;
	UINT64 freq;

	if (QueryPerformanceFrequency(&t) == FALSE)
	{
		freq = 1000ULL;
	}
	else
	{
		memcpy(&freq, &t, sizeof(UINT64));
	}

	return (double)diff / (double)freq;
}
UINT64 MsGetHiResTimeSpanUSec(UINT64 diff)
{
	LARGE_INTEGER t;
	UINT64 freq;

	if (QueryPerformanceFrequency(&t) == FALSE)
	{
		freq = 1000ULL;
	}
	else
	{
		memcpy(&freq, &t, sizeof(UINT64));
	}

	return (UINT64)(diff) * 1000ULL * 1000ULL / (UINT64)freq;
}

// Get a high-resolution counter
UINT64 MsGetHiResCounter()
{
	LARGE_INTEGER t;
	UINT64 ret;

	if (QueryPerformanceCounter(&t) == FALSE)
	{
		return 0;
	}

	memcpy(&ret, &t, sizeof(UINT64));

	return ret;
}

UINT64 TickHighres64()
{
	UINT64 ret = 0;

	ret = (UINT64)(MsGetHiResTimeSpan(MsGetHiResCounter()) * 1000.0f);

	return ret;
}

void pps_write_test()
{
	UINT target_number_ip = 67108864; // 56 - 30 = 26bit
	UINT pps = 14880000;
	UINT total_size = target_number_ip * sizeof(UINT64); // 500MB
	UCHAR *buf = malloc(total_size);
	UINT64 *buf64 = (UINT64 *)buf;

	if (buf == NULL)
	{
		printf("Malloc error.\n");
	}
	else
	{
		memset(buf, 0, total_size);
		printf("Malloc ok. %p\n", buf);
		{
			UINT current = 0;
			UINT i;

			UINT64 start = TickHighres64();
			UINT64 end;

			for (i = 0;i < 14880000;i++)
			{
				current += 12341;
				volatile UINT64 value = buf64[current % target_number_ip];
				buf64[(~current) % target_number_ip] += i;
			}

			end = TickHighres64();

			printf("write time = %I64u\n", (end - start));
		}
		free(buf);
	}
}

int main()
{
	printf("Benchmark test\n");

	pps_write_test();
}

// プログラムの実行: Ctrl + F5 または [デバッグ] > [デバッグなしで開始] メニュー
// プログラムのデバッグ: F5 または [デバッグ] > [デバッグの開始] メニュー

// 作業を開始するためのヒント: 
//    1. ソリューション エクスプローラー ウィンドウを使用してファイルを追加/管理します 
//   2. チーム エクスプローラー ウィンドウを使用してソース管理に接続します
//   3. 出力ウィンドウを使用して、ビルド出力とその他のメッセージを表示します
//   4. エラー一覧ウィンドウを使用してエラーを表示します
//   5. [プロジェクト] > [新しい項目の追加] と移動して新しいコード ファイルを作成するか、[プロジェクト] > [既存の項目の追加] と移動して既存のコード ファイルをプロジェクトに追加します
//   6. 後ほどこのプロジェクトを再び開く場合、[ファイル] > [開く] > [プロジェクト] と移動して .sln ファイルを選択します
