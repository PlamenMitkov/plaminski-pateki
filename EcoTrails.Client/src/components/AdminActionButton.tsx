import type { ButtonHTMLAttributes, ReactNode } from 'react';
import AdminOnlyAction from './AdminOnlyAction';

interface AdminActionButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  isAdmin: boolean;
  children: ReactNode;
}

function AdminActionButton({ isAdmin, children, ...buttonProps }: AdminActionButtonProps) {
  return (
    <AdminOnlyAction isAdmin={isAdmin}>
      <button type="button" className="secondary-btn" {...buttonProps}>
        {children}
      </button>
    </AdminOnlyAction>
  );
}

export default AdminActionButton;