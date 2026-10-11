export interface ConfirmOptions {
  title?: string;
  message: string;
  confirmText?: string;
  confirmLabel?: string;
  cancelText?: string;
  cancelLabel?: string;
  tone?: 'danger' | 'warning' | 'primary';
  confirmButtonClass?: string;
}

export interface ConfirmState {
  options: ConfirmOptions;
  resolve: (value: boolean) => void;
}
