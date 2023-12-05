/* eslint-disable react/prop-types */
import { Navigate, Outlet, useLocation } from "react-router-dom"
import useAuth from "../hooks/useAuth"

const RequireAuth = () => {
    const {auth} = useAuth();
    const location = useLocation();
    
  return (

    // find by user
    auth?.user? <Outlet/> : <Navigate to="/login" state={{from: location}} replace/>

    // find by role user
    // auth?.roles?.find(role => allowedRoles?.includes(role))?
    //   <Outlet/> : 
    // auth?.user?
    //   <Navigate to="/unauthorized" state={{from: location}} replace/>:
    // <Navigate to="/login" state={{from: location}} replace/>

  )
}

export default RequireAuth