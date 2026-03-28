import apiClient from './apiClient';

export interface CommunityPostAiReviewResponse {
  isLikelyTrailProposal: boolean;
  reliabilityScore: number;
  summary: string;
  suggestedName: string;
  suggestedLocation: string;
  suggestedRegion: string;
  suggestedDifficultyLevel: string;
  warnings: string[];
}

export interface CommunityPostResponse {
  id: number;
  trailId: number | null;
  trailName: string;
  title: string;
  content: string;
  postType: string;
  proposalStatus: string;
  rejectionReason: string | null;
  createdAtUtc: string;
  imageUrls: string[];
  aiReview?: CommunityPostAiReviewResponse | null;
}

export async function getMyCommunityPosts(): Promise<CommunityPostResponse[]> {
  const response = await apiClient.get<CommunityPostResponse[]>('/communityposts/mine');
  return response.data;
}

export interface CreateCommunityPostRequest {
  title: string;
  content: string;
  trailId?: number;
  postType?: string;
  images: File[];
}

export async function createCommunityPost(request: CreateCommunityPostRequest): Promise<CommunityPostResponse> {
  const formData = new FormData();
  formData.append('title', request.title);
  formData.append('content', request.content);
  if (typeof request.trailId === 'number' && request.trailId > 0) {
    formData.append('trailId', String(request.trailId));
  }

  if (request.postType) {
    formData.append('postType', request.postType);
  }

  request.images.forEach((file) => {
    formData.append('images', file);
  });

  const response = await apiClient.post<CommunityPostResponse>('/communityposts/mine', formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  });

  return response.data;
}

export interface UpdateCommunityPostRequest {
  title: string;
  content: string;
  trailId?: number;
  postType?: string;
}

export async function updateCommunityPost(
  postId: number,
  request: UpdateCommunityPostRequest
): Promise<CommunityPostResponse> {
  const response = await apiClient.put<CommunityPostResponse>(
    `/communityposts/mine/${postId}`,
    request
  );
  
  return response.data;
}

export async function deleteCommunityPost(postId: number): Promise<void> {
  await apiClient.delete(`/communityposts/mine/${postId}`);
}
