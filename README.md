NDllInjector
============
Tool for injecting .net library in native process (or not native with some limitations). Support both x86 and x64. Tested on v2.0.50727 and v4.0.30319 runtimes. For loading runtime was used interface marked as obsolete from 4.0 and later.

How to build
------------
- Set environment variable %fasm_home% to path to fasm home dir (use latest version from [http://flatassembler.net/](http://flatassembler.net/ "http://flatassembler.net/"))
- Build with VS 2010 or later.

Usage
----------
`ndllinjector <pid> <runtime version> <dll path> <class name> <function name>`

- pid - process id
- runtime version - version of .net runtime that will be loaded in process
- dll path - full path of dll to inject
- class name - full class name with namespace
- function name - function name with signature: `public static int <function name>(string arg)`
