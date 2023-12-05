import { Routes, Route } from 'react-router-dom'
// import RequireAuth from './components/RequireAuth'
import Dashboard from './app/Dashboard'
import Layout from './components/Layout'
import Login from './app/Login'
import Bank from './app/Bank'
import Admin from './app/Admin'
import Summary from './app/Summary'
import Missing from './app/Missing'
import RequireAuth from './components/RequireAuth'

export default function App() {

  return (
    <Routes>
      <Route path='login' element={<Login/>}/>
      <Route element={<RequireAuth/>}>
        <Route path='/' element={<Layout/>}>

          {/* we want to protected these routes */}
          <Route element={<RequireAuth/>}>
            <Route path='/' element={<Dashboard/>}/>
          </Route>

          <Route element={<RequireAuth/>}>
            <Route path='bank' element={<Bank/>}>
              <Route path=':bankname' element={<Bank/>}/>
            </Route>
          </Route>

          <Route element={<RequireAuth/>}>
            <Route path='admin' element={<Admin/>}/>
          </Route>

          <Route element={<RequireAuth/>}>
            <Route path='summary' element={<Summary/>}/>
          </Route>
        </Route>
      </Route>
      {/* catch all */}
      <Route path='/*' element={<Missing/>}/>
    </Routes>
  )
}