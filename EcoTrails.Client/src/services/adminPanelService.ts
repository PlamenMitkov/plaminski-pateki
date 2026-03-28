import axios from 'axios';
import { apiBaseUrl } from './apiClient';
import type { CommunityPostAiReviewResponse } from './communityService';

const PANEL_TOKEN_KEY = 'ecotrails:adminPanelToken';

export function getPanelToken(): string | null {
  return sessionStorage.getItem(PANEL_TOKEN_KEY);
}

export function setPanelToken(token: string): void {
  sessionStorage.setItem(PANEL_TOKEN_KEY, token);
}

export function clearPanelToken(): void {
  sessionStorage.removeItem(PANEL_TOKEN_KEY);
}

const panelClient = axios.create({ baseURL: apiBaseUrl });

panelClient.interceptors.request.use((config) => {
  const token = getPanelToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export interface AdminPanelProposalResponse {
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
  aiReview: CommunityPostAiReviewResponse | null;
}

export interface AdminPanelApproveRequest {
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

export async function adminPanelLogin(username: string, password: string): Promise<string> {
  const response = await panelClient.post<{ token: string }>('/adminpanel/login', { username, password });
  return response.data.token;
}

export async function getAdminProposals(status?: string): Promise<AdminPanelProposalResponse[]> {
  const params = status ? { status } : {};
  const response = await panelClient.get<AdminPanelProposalResponse[]>('/adminpanel/proposals', { params });
  return response.data;
}

export async function approveAdminProposal(
  postId: number,
  request: AdminPanelApproveRequest = {},
): Promise<{ trailId: number; trailName: string }> {
  const response = await panelClient.post<{ trailId: number; trailName: string }>(
    `/adminpanel/${postId}/approve`,
    request,
  );
  return response.data;
}

export async function rejectAdminProposal(postId: number, reason: string): Promise<void> {
  await panelClient.post(`/adminpanel/${postId}/reject`, { reason });
}
