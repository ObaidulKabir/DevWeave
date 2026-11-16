import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

/** Public marketing / auth routes — bounce signed-in owners to the tenant dashboard. */
export function GuestOnly() {
  const { authenticated } = useAuth();
  if (authenticated) {
    return <Navigate to="/apps" replace />;
  }
  return <Outlet />;
}
