import apiClient from './apiClient';
import type { CommunityPostResponse } from './communityService';

export interface TrailDataQualityResponse {
  totalTrails: number;
  missingCoordinates: number;
  missingLengthHints: number;
  missingElevationGain: number;
  missingDescription: number;
  staleSourcePreviews: number;
  generatedAtUtc: string;
}

export async function getTrailDataQuality(): Promise<TrailDataQualityResponse> {
  const response = await apiClient.get<TrailDataQualityResponse>('/trails/admin/data-quality');
  return response.data;
}

export async function triggerManualReEnrich(): Promise<void> {
  await apiClient.post('/trails/admin/re-enrich');
}

export interface ApproveTrailProposalRequest {
  name?: string;
  location?: string;
  region?: string;
  difficultyLevel?: string;
  durationInHours?: number;
  elevationGain?: number;
  latitude?: number;
  longitude?: number;
  waterSources?: boolean;
  suitableForKids?: boolean;
  maxAltitude?: number;
  requiredGearJson?: string;
}

export async function getPendingTrailProposals(): Promise<CommunityPostResponse[]> {
  const response = await apiClient.get<CommunityPostResponse[]>('/communityposts/admin/pending-trail-proposals');
  return response.data;
}

export async function approveTrailProposal(postId: number, request: ApproveTrailProposalRequest = {}): Promise<void> {
  await apiClient.post(`/communityposts/admin/${postId}/approve`, request);
}
