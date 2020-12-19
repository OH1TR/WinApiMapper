# Dumps structure of Windows API struct.

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

Or Frida dump mode:

```javascript
WinApiMapper.exe 32 LPSTARTUPINFO --frida
function Dump_STARTUPINFOW(ptr){
  console.log('_STARTUPINFOW at '+ptr);
  console.log('cb: '+ '0x'+ptr.add(0).readU32().toString(16));        //unsigned long
  console.log('lpReserved: '+ ptr.add(4).readUtf16String());        //wchar_t *
  console.log('lpDesktop: '+ ptr.add(8).readUtf16String());        //wchar_t *
  console.log('lpTitle: '+ ptr.add(12).readUtf16String());        //wchar_t *
  console.log('dwX: '+ '0x'+ptr.add(16).readU32().toString(16));        //unsigned long
  console.log('dwY: '+ '0x'+ptr.add(20).readU32().toString(16));        //unsigned long
  console.log('dwXSize: '+ '0x'+ptr.add(24).readU32().toString(16));        //unsigned long
  console.log('dwYSize: '+ '0x'+ptr.add(28).readU32().toString(16));        //unsigned long
  console.log('dwXCountChars: '+ '0x'+ptr.add(32).readU32().toString(16));        //unsigned long
  console.log('dwYCountChars: '+ '0x'+ptr.add(36).readU32().toString(16));        //unsigned long
  console.log('dwFillAttribute: '+ '0x'+ptr.add(40).readU32().toString(16));        //unsigned long
  console.log('dwFlags: '+ '0x'+ptr.add(44).readU32().toString(16));        //unsigned long
  console.log('wShowWindow: '+ '0x'+ptr.add(48).readU16().toString(16));        //unsigned short
  console.log('cbReserved2: '+ '0x'+ptr.add(50).readU16().toString(16));        //unsigned short
  console.log('lpReserved2: '+ ptr.add(52).readPointer());        //unsigned char *
  console.log('hStdInput: '+ ptr.add(56).readPointer());        //void *
  console.log('hStdOutput: '+ ptr.add(60).readPointer());        //void *
  console.log('hStdError: '+ ptr.add(64).readPointer());        //void *
}
```