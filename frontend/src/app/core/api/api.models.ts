export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
}

export interface ProblemDetails {
  code?: string;
  detail?: string;
  status?: number;
  title?: string;
}
