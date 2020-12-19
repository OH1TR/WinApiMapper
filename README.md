# Dumps structure of Windows API struct.

Injects type from command-line to template C program. Compiles the program with debug information. Reads debug information and dumps content of that type.

Needs Visual Studio with C++ workload. Run from developer command prompt.

This is totally experimental code, will not work for complex types.

Example output:

```
WinApiMapper.exe -b 64 -s LPSTARTUPINFO
_STARTUPINFOW * :
offset: 0x0 unsigned long cb
offset: 0x8 wchar_t * lpReserved
offset: 0x10 wchar_t * lpDesktop
offset: 0x18 wchar_t * lpTitle
offset: 0x20 unsigned long dwX
offset: 0x24 unsigned long dwY
offset: 0x28 unsigned long dwXSize
offset: 0x2c unsigned long dwYSize
offset: 0x30 unsigned long dwXCountChars
offset: 0x34 unsigned long dwYCountChars
offset: 0x38 unsigned long dwFillAttribute
offset: 0x3c unsigned long dwFlags
offset: 0x40 unsigned short wShowWindow
offset: 0x42 unsigned short cbReserved2
offset: 0x48 unsigned char * lpReserved2
offset: 0x50 void * hStdInput
offset: 0x58 void * hStdOutput
offset: 0x60 void * hStdError
```

Or Frida dump mode:

```javascript
WinApiMapper.exe -b 64 -m frida -s LPSTARTUPINFO
function Dump_STARTUPINFOW(ptr){
  console.log('_STARTUPINFOW at '+ptr);
  console.log('cb: '+ '0x'+ptr.add(0).readU32().toString(16));        //unsigned long
  console.log('lpReserved: '+ ptr.add(8).readUtf16String());        //wchar_t *
  console.log('lpDesktop: '+ ptr.add(16).readUtf16String());        //wchar_t *
  console.log('lpTitle: '+ ptr.add(24).readUtf16String());        //wchar_t *
  console.log('dwX: '+ '0x'+ptr.add(32).readU32().toString(16));        //unsigned long
  console.log('dwY: '+ '0x'+ptr.add(36).readU32().toString(16));        //unsigned long
  console.log('dwXSize: '+ '0x'+ptr.add(40).readU32().toString(16));        //unsigned long
  console.log('dwYSize: '+ '0x'+ptr.add(44).readU32().toString(16));        //unsigned long
  console.log('dwXCountChars: '+ '0x'+ptr.add(48).readU32().toString(16));        //unsigned long
  console.log('dwYCountChars: '+ '0x'+ptr.add(52).readU32().toString(16));        //unsigned long
  console.log('dwFillAttribute: '+ '0x'+ptr.add(56).readU32().toString(16));        //unsigned long
  console.log('dwFlags: '+ '0x'+ptr.add(60).readU32().toString(16));        //unsigned long
  console.log('wShowWindow: '+ '0x'+ptr.add(64).readU16().toString(16));        //unsigned short
  console.log('cbReserved2: '+ '0x'+ptr.add(66).readU16().toString(16));        //unsigned short
  console.log('lpReserved2: '+ ptr.add(72).readPointer());        //unsigned char *
  console.log('hStdInput: '+ ptr.add(80).readPointer());        //void *
  console.log('hStdOutput: '+ ptr.add(88).readPointer());        //void *
  console.log('hStdError: '+ ptr.add(96).readPointer());        //void *
}

```