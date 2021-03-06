// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

#include <vcclr.h>

// add headers that you want to pre-compile here

#if !defined(_DEBUG) || defined(PROJSHARP_USE_DLL)
#pragma comment(lib, "proj.lib")
#pragma comment(lib, "jpeg.lib")
#pragma comment(lib, "lzma.lib")
#pragma comment(lib, "tiff.lib")
#pragma comment(lib, "zlib.lib")
#else
#pragma comment(lib, "proj_d.lib")
#pragma comment(lib, "jpegd.lib")
#pragma comment(lib, "lzmad.lib")
#pragma comment(lib, "tiffd.lib")
#pragma comment(lib, "zlibd.lib")
#endif
#pragma comment(lib, "sqlite3.lib")

#pragma comment(lib, "Ole32.lib")
#pragma comment(lib, "Shell32.lib")

#include <proj.h>

namespace SharpProj {
	ref class ProjContext;

	namespace Proj {
		ref class Datum;
		ref class ProjObject;
	}
}

using namespace System;
using namespace SharpProj;
using namespace SharpProj::Proj;

#include <string>
std::string utf8_string(String^ v);

using Out = System::Runtime::InteropServices::OutAttribute;
using Optional = System::Runtime::InteropServices::OptionalAttribute;

#define LIT2(x) #x
#define LIT(x) LIT2(x)

#define PROJ_VERSION LIT(PROJ_VERSION_MAJOR) "." LIT(PROJ_VERSION_MINOR) "." LIT(PROJ_VERSION_PATCH)


inline static String^ Utf8_PtrToString(const char* pTxt)
{
	if (!pTxt)
		return nullptr;

	return gcnew String(pTxt, 0, strlen(pTxt), System::Text::Encoding::UTF8);
}


#endif //PCH_H
