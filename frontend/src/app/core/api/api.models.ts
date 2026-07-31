export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
}

export interface ProblemDetails {
  detail?: string;
  status?: number;
  title?: string;
}
