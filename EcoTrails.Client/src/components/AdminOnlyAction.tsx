import type { ReactNode } from 'react';

interface AdminOnlyActionProps {
  isAdmin: boolean;
  children: ReactNode;
}

function AdminOnlyAction({ isAdmin, children }: AdminOnlyActionProps) {
  if (!isAdmin) {
    return null;
  }

  return <>{children}</>;
}

export default AdminOnlyAction;