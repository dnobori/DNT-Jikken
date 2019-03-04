// Auto generated by IPA Box Test for C


#define GENERATED_CODE_C
#include "common.h"


enum CallRetAddress {
    CallRetAddress__MagicReturn,
    CallRetAddress__0x80489b1,
    CallRetAddress__0x80489f5,
}
;

#ifndef  HEADER_ONLY
void Iam_The_IntelCPU_HaHaHa(VCpuState *state, uint ip)
{
uint eax = state->Eax;
uint ebx = state->Ebx;
uint ecx = state->Ecx;
uint edx = state->Edx;
uint esp = state->Esp; 
uint esi = state->Esi; 
uint edi = state->Edi; 
uint ebp = state->Ebp; 
uint cache_last_page1 = 0xffffffff;
uint last_used_cache = 0;
byte *cache_last_realaddr1 = null;
uint cache_last_page2 = 0xffffffff;
byte *cache_last_realaddr2 = null;
uint vaddr = 0, vaddr1_index = 0, vaddr1_offset = 0;
uint write_tmp = 0, read_tmp = 0;
uint compare_result = 0;
VMemory *Memory = state->Memory;
VPageTableEntry* pte = Memory->PageTableEntry;
byte *cont_memory = Memory->ContiguousMemory;
uint cont_start = Memory->ContiguousStart;
uint cont_end = Memory->ContiguousEnd;
byte *cont_memory_minus_start = (byte *)(Memory->ContiguousMemory - cont_start);
uint next_ip = ip;
uint next_return = 0x7fffffff;
ushort *al = ((ushort*)(&eax) + 0); ushort *ah = ((ushort*)(&eax) + 1);
ushort *bl = ((ushort*)(&ebx) + 0); ushort *bh = ((ushort*)(&ebx) + 1);
ushort *cl = ((ushort*)(&ecx) + 0); ushort *ch = ((ushort*)(&ecx) + 1);
ushort *dl = ((ushort*)(&edx) + 0); ushort *dh = ((ushort*)(&edx) + 1);
const uint eiz = 0; 
char exception_string[256] = {0};
uint exception_address = 0;
byte *realaddr1 = null;
uint memcache_esp_0x0_pin = 0x7fffffff; uint memcache_esp_0x0_data = 0xcafebeef;
uint memcache_esp_0xc_pin = 0x7fffffff; uint memcache_esp_0xc_data = 0xcafebeef;
uint memcache_esp_0x10_pin = 0x7fffffff; uint memcache_esp_0x10_data = 0xcafebeef;
uint memcache_esp_0x18_pin = 0x7fffffff; uint memcache_esp_0x18_data = 0xcafebeef;


if (state->UseAsm)
{
	DYNASM_CPU_STATE dyn = { 0 };

	dyn.ContMemMinusStart = cont_memory_minus_start;
    dyn.ContStart = cont_start;
	dyn.Eax = eax;
	dyn.Ebx = ebx;
	dyn.Ecx = ecx;
	dyn.Edx = edx;
	dyn.Esi = esi;
	dyn.Edi = edi;
	dyn.Ebp = ebp;
	dyn.Esp = esp;
	dyn.StartIp = next_ip;

	dynasm(&dyn);

	eax = dyn.Eax;
	ebx = dyn.Ebx;
	ecx = dyn.Ecx;
	edx = dyn.Edx;
	esi = dyn.Esi;
	edi = dyn.Edi;
	ebp = dyn.Ebp;
	esp = dyn.Esp;

	if (dyn.ExceptionType != 0)
	{
		exception_address = dyn.ExceptionAddress;
		sprintf(exception_string, "ASM ExceptionType: % u", dyn.ExceptionType);

    }

	goto L_RETURN;
}


L_START:
switch (next_ip)
{
case 0x80488b0: goto L_80488b0;
case 0x8048910: goto L_8048910;
case 0x8048980: goto L_8048980;
case 0x80489e0: goto L_80489e0;
default:
    sprintf(exception_string, "Invalid jump target.");
    exception_address = next_ip;
    goto L_RETURN;
}

L_RET_FROM_CALL:
switch (next_return)
{
case CallRetAddress__MagicReturn: goto L_RETURN;
case CallRetAddress__0x80489b1: goto L_80489b1;
case CallRetAddress__0x80489f5: goto L_80489f5;
default:
    sprintf(exception_string, "Invalid call return target.");
    exception_address = next_ip;
    goto L_RETURN;
}

// function test_target1();
// 80488b0 push %esi
L_80488b0:
{
esp -= 4;
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80488b0;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = esi;

}

// 80488b1 push %ebx
{
esp -= 4;
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80488b1;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = ebx;

}

// 80488b2 xor %esi,%esi
{
esi = 0;
}

// 80488b4 mov $0x3,%ebx
{
ebx = ( +0x3);
}

// 80488b9 sub $0x10,%esp
{
esp -= ( +0x10);
compare_result = esp;
}

// 80488bc movl $0x4e20,0xc(%esp)
{
vaddr = (esp +0xc);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80488bc;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = ( +0x4e20);

}

// 80488c4 mov 0xc(%esp),%eax
{
vaddr = (esp +0xc);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80488c4;
    goto L_RETURN;
}
#endif // !NO_CHECK
eax= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

}

// 80488c8 cmp $0x2,%eax
{
compare_result = (uint)(eax - ( +0x2));
}

// 80488cb jbe 8048907 <test_target1+0x57>
{
if (compare_result == 0 || compare_result >= 0x80000000) {
    goto L_8048907;
}
}

// 80488cd lea 0x0(%esi),%esi
{
esi = esi;
}

// 80488d0 cmp $0x2,%ebx
L_80488d0:
{
compare_result = (uint)(ebx - ( +0x2));
}

// 80488d3 jbe 80488f9 <test_target1+0x49>
{
if (compare_result == 0 || compare_result >= 0x80000000) {
    goto L_80488f9;
}
}

// 80488d5 test $0x1,%bl
{
compare_result = (uint)(*bl & ( +0x1));
}

// 80488d8 je 80488fc <test_target1+0x4c>
{
if (compare_result == 0) {
    goto L_80488fc;
}
}

// 80488da mov $0x2,%ecx
{
ecx = ( +0x2);
}

// 80488df jmp 80488f2 <test_target1+0x42>
{
if (true) {
    goto L_80488f2;
}
}

// 80488e1 lea 0x0(%esi,%eiz,1),%esi
{
esi = (esi + eiz * 0x1);
}

// 80488e8 xor %edx,%edx
L_80488e8:
{
edx = 0;
}

// 80488ea mov %ebx,%eax
{
eax = ebx;
}

// 80488ec div %ecx
{
if (edx != 0) {
ulong tmp1 =  (uint)(((ulong)edx << 32) + (ulong)eax);
ulong tmp2 = ecx;
eax = (uint)(tmp1 / tmp2);
edx = (uint)(tmp1 - tmp2 * eax);
} else
{ 
uint tmp1 = eax;
uint tmp2 = ecx;
eax = tmp1 / tmp2;
edx = tmp1 - tmp2 * eax;
}
}

// 80488ee test %edx,%edx
{
compare_result = (uint)(edx);
}

// 80488f0 je 80488fc <test_target1+0x4c>
{
if (compare_result == 0) {
    goto L_80488fc;
}
}

// 80488f2 add $0x1,%ecx
L_80488f2:
{
ecx += ( +0x1);
compare_result = ecx;
}

// 80488f5 cmp %ebx,%ecx
{
compare_result = (uint)(ecx - ebx);
}

// 80488f7 jne 80488e8 <test_target1+0x38>
{
if (compare_result != 0) {
    goto L_80488e8;
}
}

// 80488f9 add $0x1,%esi
L_80488f9:
{
esi += ( +0x1);
compare_result = esi;
}

// 80488fc mov 0xc(%esp),%eax
L_80488fc:
{
vaddr = (esp +0xc);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80488fc;
    goto L_RETURN;
}
#endif // !NO_CHECK
eax= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

}

// 8048900 add $0x1,%ebx
{
ebx += ( +0x1);
compare_result = ebx;
}

// 8048903 cmp %ebx,%eax
{
compare_result = (uint)(eax - ebx);
}

// 8048905 jae 80488d0 <test_target1+0x20>
{
if (compare_result <= 0x80000000) {
    goto L_80488d0;
}
}

// 8048907 add $0x10,%esp
L_8048907:
{
esp += ( +0x10);
compare_result = esp;
}

// 804890a mov %esi,%eax
{
eax = esi;
}

// 804890c pop %ebx
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x804890c;
    goto L_RETURN;
}
#endif // !NO_CHECK
ebx= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
}

// 804890d pop %esi
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x804890d;
    goto L_RETURN;
}
#endif // !NO_CHECK
esi= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
}

// 804890e ret 
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x804890e;
    goto L_RETURN;
}
#endif // !NO_CHECK
next_return= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
goto L_RET_FROM_CALL;
}

// 804890f nop 
{
}

// function test_target2();
// 8048910 push %esi
L_8048910:
{
esp -= 4;
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048910;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = esi;

}

// 8048911 push %ebx
{
esp -= 4;
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048911;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = ebx;

}

// 8048912 sub $0x1f50,%esp
{
esp -= ( +0x1f50);
compare_result = esp;
}

// 8048918 movl $0x7d0,0xc(%esp)
{
vaddr = (esp +0xc);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048918;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = ( +0x7d0);

}

// 8048920 mov 0xc(%esp),%eax
{
vaddr = (esp +0xc);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048920;
    goto L_RETURN;
}
#endif // !NO_CHECK
eax= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

}

// 8048924 test %eax,%eax
{
compare_result = (uint)(eax);
}

// 8048926 je 804893e <test_target2+0x2e>
{
if (compare_result == 0) {
    goto L_804893e;
}
}

// 8048928 lea 0x10(%esp),%ebx
{
ebx = (esp +0x10);
}

// 804892c xor %eax,%eax
{
eax = 0;
}

// 804892e xchg %ax,%ax
{
}

// 8048930 mov 0xc(%esp),%edx
L_8048930:
{
vaddr = (esp +0xc);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048930;
    goto L_RETURN;
}
#endif // !NO_CHECK
edx= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

}

// 8048934 mov %eax,(%ebx,%eax,4)
{
vaddr = (ebx + eax * 0x4);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048934;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = eax;

}

// 8048937 add $0x1,%eax
{
eax += ( +0x1);
compare_result = eax;
}

// 804893a cmp %eax,%edx
{
compare_result = (uint)(edx - eax);
}

// 804893c ja 8048930 <test_target2+0x20>
{
if (compare_result != 0 && compare_result <= 0x80000000) {
    goto L_8048930;
}
}

// 804893e mov $0xc350,%esi
L_804893e:
{
esi = ( +0xc350);
}

// 8048943 xor %eax,%eax
{
eax = 0;
}

// 8048945 lea 0x0(%esi),%esi
{
esi = esi;
}

// 8048948 mov 0xc(%esp),%edx
L_8048948:
{
vaddr = (esp +0xc);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048948;
    goto L_RETURN;
}
#endif // !NO_CHECK
edx= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

}

// 804894c test %edx,%edx
{
compare_result = (uint)(edx);
}

// 804894e je 804896e <test_target2+0x5e>
{
if (compare_result == 0) {
    goto L_804896e;
}
}

// 8048950 lea 0x10(%esp),%ebx
{
ebx = (esp +0x10);
}

// 8048954 xor %edx,%edx
{
edx = 0;
}

// 8048956 lea 0x0(%esi),%esi
{
esi = esi;
}

// 8048959 lea 0x0(%edi,%eiz,1),%edi
{
edi = (edi + eiz * 0x1);
}

// 8048960 mov 0xc(%esp),%ecx
L_8048960:
{
vaddr = (esp +0xc);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048960;
    goto L_RETURN;
}
#endif // !NO_CHECK
ecx= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

}

// 8048964 add (%ebx,%edx,4),%eax
{
vaddr = (ebx + edx * 0x4);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048964;
    goto L_RETURN;
}
#endif // !NO_CHECK
eax+= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

compare_result = eax;
}

// 8048967 add $0x1,%edx
{
edx += ( +0x1);
compare_result = edx;
}

// 804896a cmp %edx,%ecx
{
compare_result = (uint)(ecx - edx);
}

// 804896c ja 8048960 <test_target2+0x50>
{
if (compare_result != 0 && compare_result <= 0x80000000) {
    goto L_8048960;
}
}

// 804896e sub $0x1,%esi
L_804896e:
{
esi -= ( +0x1);
compare_result = esi;
}

// 8048971 jne 8048948 <test_target2+0x38>
{
if (compare_result != 0) {
    goto L_8048948;
}
}

// 8048973 add $0x1f50,%esp
{
esp += ( +0x1f50);
compare_result = esp;
}

// 8048979 pop %ebx
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048979;
    goto L_RETURN;
}
#endif // !NO_CHECK
ebx= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
}

// 804897a pop %esi
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x804897a;
    goto L_RETURN;
}
#endif // !NO_CHECK
esi= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
}

// 804897b ret 
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x804897b;
    goto L_RETURN;
}
#endif // !NO_CHECK
next_return= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
goto L_RET_FROM_CALL;
}

// 804897c lea 0x0(%esi,%eiz,1),%esi
{
esi = (esi + eiz * 0x1);
}

// function test_target4();
// 8048980 push %esi
L_8048980:
{
esp -= 4;
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048980;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = esi;

}

// 8048981 push %ebx
{
esp -= 4;
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048981;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = ebx;

}

// 8048982 sub $0x4,%esp
{
esp -= ( +0x4);
compare_result = esp;
}

// 8048985 mov 0x10(%esp),%ebx
{
vaddr = (esp +0x10);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x8048985;
    goto L_RETURN;
}
#endif // !NO_CHECK
ebx= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

}

// 8048989 test %ebx,%ebx
{
compare_result = (uint)(ebx);
}

// 804898b je 80489d3 <test_target4+0x53>
{
if (compare_result == 0) {
    goto L_80489d3;
}
}

// 804898d cmp $0x1,%ebx
{
compare_result = (uint)(ebx - ( +0x1));
}

// 8048990 je 80489d7 <test_target4+0x57>
{
if (compare_result == 0) {
    goto L_80489d7;
}
}

// 8048992 xor %esi,%esi
{
esi = 0;
}

// 8048994 jmp 80489a5 <test_target4+0x25>
{
if (true) {
    goto L_80489a5;
}
}

// 8048996 lea 0x0(%esi),%esi
{
esi = esi;
}

// 8048999 lea 0x0(%edi,%eiz,1),%edi
{
edi = (edi + eiz * 0x1);
}

// 80489a0 cmp $0x1,%ebx
L_80489a0:
{
compare_result = (uint)(ebx - ( +0x1));
}

// 80489a3 je 80489c8 <test_target4+0x48>
{
if (compare_result == 0) {
    goto L_80489c8;
}
}

// 80489a5 lea -0x1(%ebx),%eax
L_80489a5:
{
eax = (ebx -0x1);
}

// 80489a8 sub $0xc,%esp
{
esp -= ( +0xc);
compare_result = esp;
}

// 80489ab push %eax
{
esp -= 4;
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489ab;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = eax;

}

// 80489ac call 8048980 <test_target4>
{
esp -= 4;
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489ac;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = (uint)CallRetAddress__0x80489b1;

if (true) {
    goto L_8048980;
}
}

// 80489b1 add $0x10,%esp
L_80489b1:
{
esp += ( +0x10);
compare_result = esp;
}

// 80489b4 add %eax,%esi
{
esi += eax;
compare_result = esi;
}

// 80489b6 sub $0x2,%ebx
{
ebx -= ( +0x2);
compare_result = ebx;
}

// 80489b9 jne 80489a0 <test_target4+0x20>
{
if (compare_result != 0) {
    goto L_80489a0;
}
}

// 80489bb add $0x4,%esp
L_80489bb:
{
esp += ( +0x4);
compare_result = esp;
}

// 80489be mov %esi,%eax
{
eax = esi;
}

// 80489c0 pop %ebx
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489c0;
    goto L_RETURN;
}
#endif // !NO_CHECK
ebx= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
}

// 80489c1 pop %esi
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489c1;
    goto L_RETURN;
}
#endif // !NO_CHECK
esi= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
}

// 80489c2 ret 
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489c2;
    goto L_RETURN;
}
#endif // !NO_CHECK
next_return= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
goto L_RET_FROM_CALL;
}

// 80489c3 nop 
{
}

// 80489c4 lea 0x0(%esi,%eiz,1),%esi
{
esi = (esi + eiz * 0x1);
}

// 80489c8 add $0x1,%esi
L_80489c8:
{
esi += ( +0x1);
compare_result = esi;
}

// 80489cb add $0x4,%esp
{
esp += ( +0x4);
compare_result = esp;
}

// 80489ce mov %esi,%eax
{
eax = esi;
}

// 80489d0 pop %ebx
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489d0;
    goto L_RETURN;
}
#endif // !NO_CHECK
ebx= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
}

// 80489d1 pop %esi
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489d1;
    goto L_RETURN;
}
#endif // !NO_CHECK
esi= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
}

// 80489d2 ret 
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489d2;
    goto L_RETURN;
}
#endif // !NO_CHECK
next_return= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
goto L_RET_FROM_CALL;
}

// 80489d3 xor %esi,%esi
L_80489d3:
{
esi = 0;
}

// 80489d5 jmp 80489bb <test_target4+0x3b>
{
if (true) {
    goto L_80489bb;
}
}

// 80489d7 mov $0x1,%esi
L_80489d7:
{
esi = ( +0x1);
}

// 80489dc jmp 80489bb <test_target4+0x3b>
{
if (true) {
    goto L_80489bb;
}
}

// 80489de xchg %ax,%ax
{
}

// function test_target3();
// 80489e0 sub $0x28,%esp
L_80489e0:
{
esp -= ( +0x28);
compare_result = esp;
}

// 80489e3 movl $0x22,0x18(%esp)
{
vaddr = (esp +0x18);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489e3;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = ( +0x22);

}

// 80489eb mov 0x18(%esp),%eax
{
vaddr = (esp +0x18);
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489eb;
    goto L_RETURN;
}
#endif // !NO_CHECK
eax= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

}

// 80489ef push %eax
{
esp -= 4;
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489ef;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = eax;

}

// 80489f0 call 8048980 <test_target4>
{
esp -= 4;
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489f0;
    goto L_RETURN;
}
#endif // !NO_CHECK
*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = (uint)CallRetAddress__0x80489f5;

if (true) {
    goto L_8048980;
}
}

// 80489f5 add $0x2c,%esp
L_80489f5:
{
esp += ( +0x2c);
compare_result = esp;
}

// 80489f8 ret 
{
vaddr = esp;
#if !NO_CHECK
if ((vaddr < cont_start || vaddr >= cont_end)){
    sprintf(exception_string, "Access violation to 0x%x.", vaddr);
    exception_address = 0x80489f8;
    goto L_RETURN;
}
#endif // !NO_CHECK
next_return= (uint)(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );

esp += 4;
goto L_RET_FROM_CALL;
}

// 80489f9 lea 0x0(%esi,%eiz,1),%esi
{
esi = (esi + eiz * 0x1);
}

 // Restore CPU state
L_RETURN:
 state->Eax = eax;  state->Ebx = ebx;  state->Ecx = ecx;  state->Edx = edx; 
 state->Esi = esi;  state->Edi = edi;  state->Ebp = ebp;  state->Esp = esp; 
 strcpy(state->ExceptionString, exception_string);
 state->ExceptionAddress = exception_address;
}
#endif

enum FunctionTable
{
    FunctionTable_test_target1 = 0x80488b0,
    FunctionTable_test_target2 = 0x8048910,
    FunctionTable_test_target4 = 0x8048980,
    FunctionTable_test_target3 = 0x80489e0,
}
;

