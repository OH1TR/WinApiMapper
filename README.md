# Dumps Windows API struct with offsets, field names and types.

Injects type from command-line to template C program. Compiles the program with debug information. Reads debug information and dumps content of that type.

Needs Visual Studio with C++ workload. Run from developer command prompt.

This is totally experimental code, will not work for complex types.

Example output:

```
WinApiMapper.exe 32 LPSTARTUPINFO
_STARTUPINFOW * :
offset: 0x0 unsigned long cb
offset: 0x4 wchar_t * lpReserved
offset: 0x8 wchar_t * lpDesktop
offset: 0xc wchar_t * lpTitle
offset: 0x10 unsigned long dwX
offset: 0x14 unsigned long dwY
offset: 0x18 unsigned long dwXSize
offset: 0x1c unsigned long dwYSize
offset: 0x20 unsigned long dwXCountChars
offset: 0x24 unsigned long dwYCountChars
offset: 0x28 unsigned long dwFillAttribute
offset: 0x2c unsigned long dwFlags
offset: 0x30 unsigned short wShowWindow
offset: 0x32 unsigned short cbReserved2
offset: 0x34 unsigned char * lpReserved2
offset: 0x38 void * hStdInput
offset: 0x3c void * hStdOutput
offset: 0x40 void * hStdError
```
