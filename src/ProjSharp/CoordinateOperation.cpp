#include "pch.h"
#include "ProjContext.h"
#include "CoordinateOperation.h"
#include "CoordinateOperationList.h"
#include "CoordinateReferenceSystem.h"
#include "CoordinateArea.h"
#include "ProjException.h"
using namespace ProjSharp;

CoordinateOperation^ CoordinateOperation::Create(CoordinateReferenceSystem^ sourceCrs, CoordinateReferenceSystem^ targetCrs, CoordinateArea^ area, ProjContext^ ctx)
{
    CoordinateTransformOptions^ opts = gcnew CoordinateTransformOptions();
    opts->Area = area;

    return CoordinateOperation::Create(sourceCrs, targetCrs, opts, ctx);
}

CoordinateOperation^ CoordinateOperation::Create(CoordinateReferenceSystem^ sourceCrs, CoordinateReferenceSystem^ targetCrs, CoordinateTransformOptions ^options, ProjContext^ ctx)
{
	if (!sourceCrs)
		throw gcnew ArgumentNullException("sourceCrs");
	else if (!targetCrs)
		throw gcnew ArgumentNullException("targetCrs");
	
    if (!ctx) // After fromCrs
		ctx = sourceCrs->Context;

    if (!options)
        options = gcnew CoordinateTransformOptions();

    std::string s_auth;
    if (!String::IsNullOrEmpty(options->Authority))
        s_auth = utf8_string(options->Authority);

    auto operation_ctx = proj_create_operation_factory_context(ctx, s_auth.size() ? s_auth.c_str() : nullptr);
    if (!operation_ctx) {
        return nullptr;
    }

    proj_operation_factory_context_set_allow_ballpark_transformations(ctx, operation_ctx, !options->NoBallparkConversions);

    if (options->Accuracy.HasValue)
        proj_operation_factory_context_set_desired_accuracy(ctx, operation_ctx, options->Accuracy.Value);

    if (options && options->Area)
    {
        proj_operation_factory_context_set_area_of_interest(
            ctx,
            operation_ctx,
            options->Area->WestLongitude,
            options->Area->SouthLatitude,
            options->Area->EastLongitude,
            options->Area->NorthLatitude);
    }
    else
    {
        proj_operation_factory_context_set_spatial_criterion(
            ctx, operation_ctx,
            PROJ_SPATIAL_CRITERION_PARTIAL_INTERSECTION);
    }

    proj_operation_factory_context_set_grid_availability_use(
        ctx, operation_ctx,
        proj_context_is_network_enabled(ctx)
            ? PROJ_GRID_AVAILABILITY_KNOWN_AVAILABLE
            : (options->NoDiscardIfMissing
                ? PROJ_GRID_AVAILABILITY_USED_FOR_SORTING
                : PROJ_GRID_AVAILABILITY_DISCARD_OPERATION_IF_MISSING_GRID));


    proj_operation_factory_context_set_use_proj_alternative_grid_names(
        ctx, operation_ctx,
        !options->UsePrimaryGridNames);

    proj_operation_factory_context_set_allow_use_intermediate_crs(
        ctx, operation_ctx,
        (options->IntermediateCrsUsage == IntermediateCrsUsage::Auto
            ? PROJ_INTERMEDIATE_CRS_USE_IF_NO_DIRECT_TRANSFORMATION
            : (options->IntermediateCrsUsage == IntermediateCrsUsage::Never
                ? PROJ_INTERMEDIATE_CRS_USE_NEVER
                : PROJ_INTERMEDIATE_CRS_USE_IF_NO_DIRECT_TRANSFORMATION)));

    proj_operation_factory_context_set_discard_superseded(
        ctx, operation_ctx,
        !options->UseSuperseded);

    auto op_list = proj_create_operations(ctx, sourceCrs, targetCrs, operation_ctx);
    proj_operation_factory_context_destroy(operation_ctx);

    if (!op_list) {
        return nullptr;
    }

    auto op_count = proj_list_get_count(op_list);
    if (op_count == 0) {
        proj_list_destroy(op_list);

        throw gcnew ProjException("No operation found matching criteria");
    }

    PJ* P = proj_list_get(ctx, op_list, 0);

    if (P == nullptr || op_count == 1 || (options->Area) ||
        sourceCrs->Type == ProjType::GeocentricCrs ||
        targetCrs->Type == ProjType::GeocentricCrs) 
    {
        proj_list_destroy(op_list);
        return static_cast<CoordinateOperation^>(ctx->Create(P));
    }

    return gcnew CoordinateOperationList(ctx, P, op_list);
}

double CoordinateOperation::RoundTrip(bool forward, int transforms, array<double>^ coordinate)
{
    PJ_COORD coord;
    SetCoordinate(coord, coordinate);

    return proj_roundtrip(this, forward ? PJ_FWD : PJ_INV, transforms, &coord);
}

CoordinateOperationFactors^ CoordinateOperation::Factors(array<double>^ coordinate)
{
    PJ_COORD coord;
    SetCoordinate(coord, coordinate);

    PJ_FACTORS f = proj_factors(this, coord);

    return gcnew CoordinateOperationFactors(this, &f);
}


array<double>^ CoordinateOperation::DoTransform(bool forward, array<double>^ coordinate)
{
    if (!coordinate)
        throw gcnew ArgumentNullException("coordinate");

	PJ_COORD coord;
	SetCoordinate(coord, coordinate);

	coord = proj_trans(this, forward ? PJ_FWD : PJ_INV, coord);

	return FromCoordinate(coord, coordinate->Length);
}

double CoordinateOperation::EllipsoidDistance(array<double>^ coordinate1, array<double>^ coordinate2)
{
	PJ_COORD coord1, coord2;
	SetCoordinate(coord1, coordinate1);
	SetCoordinate(coord2, coordinate2);

	return proj_lp_dist(this, coord1, coord2);
}

double CoordinateOperation::EllipsoidDistanceZ(array<double>^ coordinate1, array<double>^ coordinate2)
{
	PJ_COORD coord1, coord2;
	SetCoordinate(coord1, coordinate1);
	SetCoordinate(coord2, coordinate2);

	return proj_lpz_dist(this, coord1, coord2);
}


array<double>^ CoordinateOperation::EllipsoidGeod(array<double>^ coordinate1, array<double>^ coordinate2)
{
	PJ_COORD coord1, coord2;
	SetCoordinate(coord1, coordinate1);
	SetCoordinate(coord2, coordinate2);

	PJ_COORD r = proj_geod(this, coord1, coord2);

	return FromCoordinate(r, 3);
}


CoordinateReferenceSystem^ CoordinateOperation::GetSourceCoordinateReferenceSystem([Optional] ProjContext^ context)
{
    if (!context)
        context = Context;

    PJ* pj = proj_get_source_crs(context, this);

    if (!pj)
        return nullptr;

    return static_cast<CoordinateReferenceSystem^>(context->Create(pj));
}


CoordinateReferenceSystem^ CoordinateOperation::GetTargetCoordinateReferenceSystem([Optional] ProjContext^ context)
{
    if (!context)
        context = Context;

    PJ* pj = proj_get_target_crs(context, this);

    if (!pj)
        return nullptr;

    return static_cast<CoordinateReferenceSystem^>(context->Create(pj));
}
