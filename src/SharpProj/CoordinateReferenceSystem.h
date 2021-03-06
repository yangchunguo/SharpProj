#pragma once
#include "ProjObject.h"

namespace SharpProj {
	using System::Collections::ObjectModel::ReadOnlyCollection;
	ref class CoordinateTransform;

	namespace Proj
	{
		ref class Axis;
		ref class AxisCollection;
		ref class Datum;
		ref class Ellipsoid;
		ref class PrimeMeridian;

		ref class GeodeticCRS;
		ref class GeometricCRS;

		ref class CoordinateSystem;
	}

	public ref class CoordinateReferenceSystem : ProjObject
	{
	private:
		ProjContext^ m_ctx;
		CoordinateSystem^ m_cs;
		GeodeticCRS^ m_geodCRS;
		Proj::Ellipsoid^ m_ellipsoid;
		Proj::Datum^ m_datum;
		Proj::PrimeMeridian^ m_primeMeridian;
		CoordinateReferenceSystem^ m_baseCrs;
		CoordinateTransform^ m_distanceTransform;
		int m_axis;

		~CoordinateReferenceSystem();

	internal:
		CoordinateReferenceSystem(ProjContext^ ctx, PJ* pj)
			: ProjObject(ctx, pj)
		{
		}


	public:
		property bool IsDeprecated
		{
			bool get()
			{
				return proj_is_deprecated(this);
			}
		}

		property CoordinateSystem^ CoordinateSystem
		{
			Proj::CoordinateSystem^ get();
		}

	public:
		CoordinateReferenceSystem^ Clone([Optional]ProjContext^ ctx)
		{
			return static_cast<CoordinateReferenceSystem^>(__super::Clone(ctx));
		}

	public:
		property int AxisCount
		{
			virtual int get();
		internal:
			void set(int value);
		}

		property Proj::AxisCollection^ Axis
		{
			virtual Proj::AxisCollection^ get();
		}

		property Proj::Ellipsoid^ Ellipsoid
		{
			Proj::Ellipsoid^ get();
		}

		property Proj::GeodeticCRS^ GeodeticCRS
		{
			Proj::GeodeticCRS^ get();
		}

		property Proj::Datum^ Datum
		{
			Proj::Datum^ get();
		}

		property Proj::PrimeMeridian^ PrimeMeridian
		{
			Proj::PrimeMeridian^ get();
		}

		property CoordinateReferenceSystem^ BaseCRS
		{
			CoordinateReferenceSystem^ get();
		}

		property CoordinateTransform^ DistanceTransform
		{
			CoordinateTransform^ get();
		}

	public:
		CoordinateReferenceSystem^ WithAxisNormalized([Optional] ProjContext^ context);

	public:
		static CoordinateReferenceSystem^ Create(String^ from, [Optional] ProjContext^ ctx);
		static CoordinateReferenceSystem^ Create(array<String^>^ from, [Optional] ProjContext^ ctx);
		static CoordinateReferenceSystem^ CreateFromWellKnownText(String^ from, [Optional] ProjContext^ ctx);
		static CoordinateReferenceSystem^ CreateFromWellKnownText(String^ from, [Out] array<String^>^% warnings, [Optional] ProjContext^ ctx);
	};
}
