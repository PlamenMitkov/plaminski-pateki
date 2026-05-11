import fs from "node:fs";
import path from "node:path";
import { PrismaClient } from "@prisma/client";
import { PrismaPg } from "@prisma/adapter-pg";

const databaseUrl = process.env.DATABASE_URL;
if (!databaseUrl) {
  throw new Error("DATABASE_URL is not set.");
}

const adapter = new PrismaPg({ connectionString: databaseUrl });
const prisma = new PrismaClient({ adapter });

function toStringArray(value) {
  if (value == null) {
    return [];
  }

  if (Array.isArray(value)) {
    return value
      .map((item) => (item == null ? "" : String(item).trim()))
      .filter((item) => item.length > 0);
  }

  const text = String(value).trim();
  if (!text) {
    return [];
  }

  if (text.includes("|")) {
    return text
      .split("|")
      .map((item) => item.trim())
      .filter((item) => item.length > 0);
  }

  return [text];
}

function toNullableNumber(value) {
  if (value == null) {
    return null;
  }

  if (typeof value === "number") {
    return Number.isFinite(value) ? value : null;
  }

  const normalized = String(value).trim().replace(",", ".");
  if (!normalized) {
    return null;
  }

  const parsed = Number.parseFloat(normalized);
  return Number.isFinite(parsed) ? parsed : null;
}

function toNullableInt(value) {
  const parsed = toNullableNumber(value);
  if (parsed == null) {
    return null;
  }

  return Number.isInteger(parsed) ? parsed : Math.trunc(parsed);
}

function toNullableDate(value) {
  if (value == null) {
    return null;
  }

  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? null : value;
  }

  const input = String(value).trim();
  if (!input) {
    return null;
  }

  const normalized = input.includes(" ") ? input.replace(" ", "T") : input;
  const parsed = new Date(normalized);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function hasAtLeastOneValue(obj) {
  return Object.values(obj).some((value) => {
    if (Array.isArray(value)) {
      return value.length > 0;
    }

    return value !== null && value !== undefined && value !== "";
  });
}

function normalizeAttractions(value) {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .map((item) => {
      if (typeof item === "string") {
        const name = item.trim();
        return name ? { name, type: null } : null;
      }

      if (item && typeof item === "object") {
        const name = String(item.name ?? "").trim();
        if (!name) {
          return null;
        }

        const typeText = String(item.type ?? "").trim();
        return {
          name,
          type: typeText || null
        };
      }

      return null;
    })
    .filter(Boolean);
}

async function main() {
  const sourcePath = process.env.ECO_JSON_PATH
    ? path.resolve(process.cwd(), process.env.ECO_JSON_PATH)
    : path.resolve(process.cwd(), "ecoupdated.json");

  if (!fs.existsSync(sourcePath)) {
    throw new Error(`Input file not found: ${sourcePath}`);
  }

  const rawJson = fs.readFileSync(sourcePath, "utf8");
  const parsed = JSON.parse(rawJson);
  const trails = Array.isArray(parsed?.eco_trails) ? parsed.eco_trails : [];

  await prisma.trail.deleteMany();

  let created = 0;
  for (const item of trails) {
    const location = item?.location ?? {};
    const coordinates = location?.coordinates ?? {};
    const trailDetails = item?.trail_details ?? {};
    const transportation = item?.transportation ?? {};
    const accessibility = item?.accessibility ?? {};
    const seasonalInfo = item?.seasonal_info ?? {};
    const contactInfo = item?.contact_info ?? {};
    const rating = item?.rating ?? {};
    const metadata = item?.metadata ?? {};

    const locationData = {
      region: location?.region ? String(location.region).trim() : null,
      nearestTown: location?.nearest_town ? String(location.nearest_town).trim() : null,
      latitude: toNullableNumber(coordinates?.latitude),
      longitude: toNullableNumber(coordinates?.longitude)
    };

    const detailsData = {
      lengthKm: toNullableNumber(trailDetails?.length_km),
      durationText: trailDetails?.duration ? String(trailDetails.duration).trim() : null,
      difficultyText: trailDetails?.difficulty ? String(trailDetails.difficulty).trim() : null,
      routeType: trailDetails?.route_type ? String(trailDetails.route_type).trim() : null,
      establishedYear: trailDetails?.established_year ? String(trailDetails.established_year).trim() : null
    };

    const transportationData = {
      publicTransport: transportation?.public_transport ? String(transportation.public_transport).trim() : null,
      parkingAvailable:
        typeof transportation?.parking_available === "boolean"
          ? transportation.parking_available
          : null
    };

    const accessibilityData = {
      wheelchairAccessible:
        typeof accessibility?.wheelchair_accessible === "boolean"
          ? accessibility.wheelchair_accessible
          : null,
      strollerFriendly:
        typeof accessibility?.stroller_friendly === "boolean"
          ? accessibility.stroller_friendly
          : null,
      bicycleAllowed:
        typeof accessibility?.bicycle_allowed === "boolean"
          ? accessibility.bicycle_allowed
          : null
    };

    const seasonalInfoData = {
      bestMonths: toStringArray(seasonalInfo?.best_months),
      winterAccessible:
        typeof seasonalInfo?.winter_accessible === "boolean"
          ? seasonalInfo.winter_accessible
          : null,
      weatherDependent:
        typeof seasonalInfo?.weather_dependent === "boolean"
          ? seasonalInfo.weather_dependent
          : null
    };

    const contactInfoData = {
      phone: contactInfo?.phone ? String(contactInfo.phone).trim() : null,
      email: contactInfo?.email ? String(contactInfo.email).trim() : null,
      website: contactInfo?.website ? String(contactInfo.website).trim() : null
    };

    const ratingData = {
      averageScore: toNullableNumber(rating?.average_score),
      totalReviews: toNullableInt(rating?.total_reviews),
      lastUpdated: toNullableDate(rating?.last_updated)
    };

    const metadataData = {
      lastVerified: toNullableDate(metadata?.last_verified),
      dataSource: metadata?.data_source ? String(metadata.data_source).trim() : null,
      status: metadata?.status ? String(metadata.status).trim() : null
    };

    const attractions = normalizeAttractions(item?.attractions);

    const data = {
      sourceTrailId: toNullableInt(item?.id),
      name: String(item?.name ?? "").trim(),
      description: item?.description ? String(item.description).trim() : null,
      shortSummary: item?.short_summary ? String(item.short_summary).trim() : null,
      sourceUrl: item?.source ? String(item.source).trim() : null,
      photoUrl: item?.photo_url ? String(item.photo_url).trim() : null,
      bestSeason: toStringArray(item?.best_season),
      suitability: toStringArray(item?.suitability),
      equipmentNeeded: toStringArray(item?.equipment_needed),
      safetyWarnings: toStringArray(item?.safety_warnings),
      nearbyAmenities: toStringArray(item?.nearby_amenities),
      locationKeywords: toStringArray(location?.keywords)
    };

    if (!data.name) {
      continue;
    }

    await prisma.trail.create({
      data: {
        ...data,
        ...(hasAtLeastOneValue(locationData) ? { location: { create: locationData } } : {}),
        ...(hasAtLeastOneValue(detailsData) ? { details: { create: detailsData } } : {}),
        ...(hasAtLeastOneValue(transportationData)
          ? { transportation: { create: transportationData } }
          : {}),
        ...(hasAtLeastOneValue(accessibilityData)
          ? { accessibility: { create: accessibilityData } }
          : {}),
        ...(hasAtLeastOneValue(seasonalInfoData)
          ? { seasonalInfo: { create: seasonalInfoData } }
          : {}),
        ...(hasAtLeastOneValue(contactInfoData) ? { contactInfo: { create: contactInfoData } } : {}),
        ...(hasAtLeastOneValue(ratingData) ? { rating: { create: ratingData } } : {}),
        ...(hasAtLeastOneValue(metadataData) ? { metadata: { create: metadataData } } : {}),
        ...(attractions.length > 0 ? { attractions: { create: attractions } } : {})
      }
    });

    created += 1;
  }

  console.log(`Imported ${created} trails from ${sourcePath}`);
}

main()
  .catch((error) => {
    console.error(error);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
