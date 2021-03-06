#include "pch.h"

#include <locale>
#include <codecvt>
#include "ProjContext.h"
#include "ProjException.h"

using namespace SharpProj;
using namespace System::IO;

std::string utf8_string(String^ v)
{
	std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> conv;
	pin_ptr<const wchar_t> pPath = PtrToStringChars(v);
	std::wstring vstr(pPath);
	std::string sstr(conv.to_bytes(vstr));
	return sstr;
}

const char* ProjContext::utf8_string(String^ value)
{
	void* chain = m_chain;
	try
	{
		return utf8_chain(value, chain);
	}
	finally
	{
		m_chain = chain;
	}
}

const char* ProjContext::utf8_chain(String^ value, void*& chain)
{
	std::string v = ::utf8_string(value);
	int slen = v.length() + 1;
	void** pp = (void**)malloc(slen + sizeof(void*));
	pp[0] = chain;
	chain = pp;
	return (char*)memcpy(&pp[1], v.c_str(), slen);
}

void ProjContext::free_chain(void*& chain)
{
	while (chain)
	{
		void* next = ((void**)chain)[0];
		free(chain);

		chain = next;
	}
}

static const char* my_file_finder(PJ_CONTEXT* ctx, const char* file, void* user_data)
{
	gcroot<WeakReference<ProjContext^>^>& ref = *(gcroot<WeakReference<ProjContext^>^>*)user_data;

	ProjContext^ pc;
	if (ref->TryGetTarget(pc))
	{
		String^ origFile = Utf8_PtrToString(file);

		String^ newFile;
		pc->OnFindFile(origFile, newFile);

		if (newFile && !origFile->Equals(newFile))
			return pc->utf8_string(newFile);
		else
			return file;
	}
	return file;
}

static void my_log_func(void* user_data, int level, const char* message)
{
	gcroot<WeakReference<ProjContext^>^>& ref = *(gcroot<WeakReference<ProjContext^>^>*)user_data;

	ProjContext^ pc;
	if (ref->TryGetTarget(pc))
	{
		String^ msg = Utf8_PtrToString(message);

		if (level == PJ_LOG_ERROR)
			pc->m_lastError = msg;
		else
			pc->m_lastError = nullptr;

		pc->OnLogMessage((ProjLogLevel)level, msg);
	}
}



ProjContext::ProjContext()
{
	m_ctx = proj_context_create();

	if (m_ctx)
	{
		WeakReference<ProjContext^>^ wr = gcnew WeakReference<ProjContext^>(this);
		m_ref = new gcroot<WeakReference<ProjContext^>^>(wr);

		proj_context_set_file_finder(m_ctx, my_file_finder, m_ref);
		proj_log_func(m_ctx, m_ref, my_log_func);
		proj_log_level(m_ctx, PJ_LOG_ERROR);

		SetupNetworkHandling();

		if (EnableNetworkConnectionsOnNewContexts)
			AllowNetworkConnections = true;
	}
}

inline SharpProj::ProjContext::~ProjContext()
{
	if (m_ctx)
	{
		proj_context_destroy(m_ctx);
		m_ctx = nullptr;
	}
	if (m_ref)
	{
		delete m_ref;
		m_ref = nullptr;
	}

	void* chain = m_chain;
	try
	{
		free_chain(chain);
	}
	finally
	{
		m_chain = chain;
	}
}

String^ ProjContext::GetMetaData(String^ key)
{
	if (String::IsNullOrEmpty(key))
		throw gcnew ArgumentNullException("key");

	std::string skey = utf8_string(key);

	const char* v = proj_context_get_database_metadata(this, skey.c_str());

	if (!v)
		throw gcnew ArgumentOutOfRangeException("key");

	return Utf8_PtrToString(v);
}
Version^ ProjContext::EpsgVersion::get()
{
	try
	{
		String^ md = GetMetaData("EPSG.VERSION");

		if (md->StartsWith("v"))
			return gcnew System::Version(md->Substring(1));
	}
	catch (ArgumentException^)
	{
	}
	return nullptr;
}


Exception^ ProjContext::ConstructException()
{
	int err = proj_context_errno(this);

	String^ msg = m_lastError;

	if (msg)
	{
		m_lastError = nullptr;
		return gcnew ProjException(Utf8_PtrToString(proj_errno_string(err)),
			gcnew ProjException(msg));
	}
	else
	{
		return gcnew ProjException(Utf8_PtrToString(proj_errno_string(err)));
	}
}

String^ ProjContext::EnvCombine(String^ envVar, String^ file)
{
	try
	{
		return Path::GetFullPath(Path::Combine(Environment::GetEnvironmentVariable("PROJ_LIB"), file));
	}
	catch(IOException^)
	{
		return file;
	}
	catch (ArgumentException^)
	{
		return file;
	}
}

void ProjContext::OnFindFile(String^ file, [Out] String^% foundFile)
{
	String^ testFile;
	const char* pUserDir = proj_context_get_user_writable_directory(this, false);
	String^ userDir = Utf8_PtrToString(pUserDir);

	if (File::Exists(testFile = Path::Combine(userDir, file)))
		foundFile = Path::GetFullPath(testFile);
	else if (File::Exists(testFile = EnvCombine("PROJ_LIB", file)))
		foundFile = Path::GetFullPath(testFile);
	else if (File::Exists(testFile = Path::Combine(userDir, ("proj" PROJ_VERSION "-") + file)))
		foundFile = Path::GetFullPath(testFile);
	else if (File::Exists(file))
		foundFile = Path::GetFullPath(file);
	else if (File::Exists(file = Path::Combine("..", file)))
		foundFile = Path::GetFullPath(file);
	else if (proj_context_is_network_enabled(this))
	{
		testFile = testFile = Path::Combine(userDir, ("proj" PROJ_VERSION "-") + file);
		try
		{
			DownloadProjDB(testFile);
		}
		catch (IOException^)
		{
			foundFile = nullptr;
			return;
		}
		catch (System::Net::WebException^)
		{
			foundFile = nullptr;
			return;
		}
		catch (InvalidOperationException^)
		{
			foundFile = nullptr;
			return;
		}

		if (File::Exists(testFile))
			foundFile = Path::GetFullPath(testFile);
	}
	else
		foundFile = nullptr;
}

void ProjContext::OnLogMessage(ProjLogLevel level, String^ message)
{
#ifdef _DEBUG
	if (level <= ProjLogLevel::Error)
		System::Diagnostics::Debug::WriteLine(message);
#endif

	OnLog(level, message);
}
