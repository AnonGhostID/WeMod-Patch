#ifndef VER_H
#define VER_H
#endif

#include <Windows.h>

#define SENTINEL_SIZE 0x20

BOOL is_wemod() {
    char path[MAX_PATH];
    DWORD offset = GetModuleFileNameA(NULL, &path[0], MAX_PATH);
    return offset >= 9 && *(DWORD64*)&path[offset - 9] == 0x78652E646F4D6557;
}

char* find_fuse(void) {
    char* ptr = (char*)GetModuleHandleA(NULL);
    IMAGE_NT_HEADERS* p_nt_headers = (IMAGE_NT_HEADERS*)(ptr + ((IMAGE_DOS_HEADER*)ptr)->e_lfanew);
    char* end_ptr = ptr + p_nt_headers->OptionalHeader.SizeOfImage - SENTINEL_SIZE;

    for (; ptr < end_ptr; ptr++) {
        if (*ptr != 0x64) continue;
#if _WIN64
        if (((DWORD64*)ptr)[0] != 0x6E64474B70374C64 || 
            ((DWORD64*)ptr)[1] != 0x6262503639377A4E ||
            ((DWORD64*)ptr)[2] != 0x58486D4B4E57516A ||
            ((DWORD64*)ptr)[3] != 0x5873743942615A42) continue;
#else
        if (((DWORD*)ptr)[0] != 0x70374C64 || ((DWORD*)ptr)[1] != 0x6E64474B ||
            ((DWORD*)ptr)[2] != 0x39377A4E || ((DWORD*)ptr)[3] != 0x62625036 ||
            ((DWORD*)ptr)[4] != 0x4E57516A || ((DWORD*)ptr)[5] != 0x58486D4B ||
            ((DWORD*)ptr)[6] != 0x42615A42 || ((DWORD*)ptr)[7] != 0x58737439) continue;
#endif
        return ptr + SENTINEL_SIZE;
    }

    return NULL;
}

#define PROXY_TRAMPOLINE(name) \
	void* p##name; \
	__declspec(dllexport) __attribute__((naked)) void name() \
	{ \
		__asm jmp qword ptr [p##name] \
	}

PROXY_TRAMPOLINE(GetFileVersionInfoA)
PROXY_TRAMPOLINE(GetFileVersionInfoByHandle)
PROXY_TRAMPOLINE(GetFileVersionInfoExA)
PROXY_TRAMPOLINE(GetFileVersionInfoExW)
PROXY_TRAMPOLINE(GetFileVersionInfoSizeA)
PROXY_TRAMPOLINE(GetFileVersionInfoSizeExA)
PROXY_TRAMPOLINE(GetFileVersionInfoSizeExW)
PROXY_TRAMPOLINE(GetFileVersionInfoSizeW)
PROXY_TRAMPOLINE(GetFileVersionInfoW)
PROXY_TRAMPOLINE(VerFindFileA)
PROXY_TRAMPOLINE(VerFindFileW)
PROXY_TRAMPOLINE(VerInstallFileA)
PROXY_TRAMPOLINE(VerInstallFileW)
PROXY_TRAMPOLINE(VerLanguageNameA)
PROXY_TRAMPOLINE(VerLanguageNameW)
PROXY_TRAMPOLINE(VerQueryValueA)
PROXY_TRAMPOLINE(VerQueryValueW)

void init_proxy(void) {
    char path[MAX_PATH];
    DWORD offset = GetSystemDirectoryA(path, MAX_PATH);
    *(DWORD*)&path[offset] = 0x7265765C;
    *(DWORD*)&path[offset + 4] = 0x6E6F6973;
    *(DWORD*)&path[offset + 8] = 0x6C6C642E;
    path[offset + 12] = 0;

    HMODULE handle = LoadLibraryA(&path[0]);
    pGetFileVersionInfoA = (void*)GetProcAddress(handle, "GetFileVersionInfoA");
    pGetFileVersionInfoByHandle = (void*)GetProcAddress(handle, "GetFileVersionInfoByHandle");
    pGetFileVersionInfoExA = (void*)GetProcAddress(handle, "GetFileVersionInfoExA");
    pGetFileVersionInfoExW = (void*)GetProcAddress(handle, "GetFileVersionInfoExW");
    pGetFileVersionInfoSizeA = (void*)GetProcAddress(handle, "GetFileVersionInfoSizeA");
    pGetFileVersionInfoSizeExA = (void*)GetProcAddress(handle, "GetFileVersionInfoSizeExA");
    pGetFileVersionInfoSizeExW = (void*)GetProcAddress(handle, "GetFileVersionInfoSizeExW");
    pGetFileVersionInfoSizeW = (void*)GetProcAddress(handle, "GetFileVersionInfoSizeW");
    pGetFileVersionInfoW = (void*)GetProcAddress(handle, "GetFileVersionInfoW");
    pVerFindFileA = (void*)GetProcAddress(handle, "VerFindFileA");
    pVerFindFileW = (void*)GetProcAddress(handle, "VerFindFileW");
    pVerInstallFileA = (void*)GetProcAddress(handle, "VerInstallFileA");
    pVerInstallFileW = (void*)GetProcAddress(handle, "VerInstallFileW");
    pVerLanguageNameA = (void*)GetProcAddress(handle, "VerLanguageNameA");
    pVerLanguageNameW = (void*)GetProcAddress(handle, "VerLanguageNameW");
    pVerQueryValueA = (void*)GetProcAddress(handle, "VerQueryValueA");
    pVerQueryValueW = (void*)GetProcAddress(handle, "VerQueryValueW");
}

BOOL APIENTRY dll_main(HMODULE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
    if (fdwReason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hinstDLL);
        init_proxy();
        if (!is_wemod()) return TRUE;
        
        char* p_fuse = find_fuse();
        if (p_fuse != NULL) {
            if (p_fuse[0] != 0x01 || p_fuse[1] < 0x05) {
                MessageBoxA(0, "Unsupported Fuse version", "Error", MB_ICONERROR);
            } else {
                DWORD protection;
                VirtualProtect(p_fuse, 0x07, PAGE_READWRITE, &protection);
                p_fuse[6] = 0x72;
                VirtualProtect(p_fuse, 0x07, protection, &protection);
                return TRUE;
            }
        }
        else {
            MessageBoxA(0, "Fuse not found", "Error", MB_ICONERROR);
        }

        ExitProcess(0);
    }
    return TRUE;
}

