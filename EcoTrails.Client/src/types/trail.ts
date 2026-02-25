export interface Trail {
  id: number;
  name: string;
  description: string;
  location: string;
  difficulty: number;
  durationInHours: number;
  elevationGain: number;
  latitude: number | null;
  longitude: number | null;
}