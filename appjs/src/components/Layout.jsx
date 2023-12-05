import { faBank, faBars, faChartBar, faHistory, faSignOut, faXmark } from '@fortawesome/free-solid-svg-icons';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { useContext, useState } from 'react';
import { Link, Outlet, useNavigate } from 'react-router-dom';
import AuthContext from '../context/AuthProvider';
import { faIntercom } from '@fortawesome/free-brands-svg-icons';
import Swal from 'sweetalert2';

const Sidebar = () => {
  const [show, setShow] = useState(true);

  const { setAuth } = useContext(AuthContext);
    const navigate = useNavigate();

    const logout = async () => {
      Swal.fire({
        title: "Are you sure?",
        text: "You won't be able to revert this!",
        icon: "warning",
        showCancelButton: true,
        confirmButtonColor: "#3085d6",
        cancelButtonColor: "#d33",
        confirmButtonText: "Logout?"
      }).then((result) => {
        if (result.isConfirmed) {
          setAuth({});
          localStorage.clear()
          navigate('/login');
        }
      });
        
    }

  return (
    <main className={show ? 'space-toggle' : null}>
        <header className={`header ${show ? 'space-toggle' : null}`}>
          <div className='header-toggle' onClick={() => setShow(!show)}>
            {!show? <FontAwesomeIcon icon={faBars}/> : <FontAwesomeIcon icon={faXmark}/>}
          </div>
        </header>

        <aside className={`shadow-lg sidebar ${show ? 'show' : null}`}>
          <nav className='nav'>
            <div>
              <Link to='/' className='nav-logo'>
                <img src="/img/logo-s21.png" alt="logo s21" style={{width: "38px"}}/>
                <span className='nav-logo-name'>H2H</span>
              </Link>

              <div className='nav-list'>
                <Link to='/' className='nav-link font-bold'>
                  <FontAwesomeIcon icon={faChartBar}/>
                  <span className={`nav-link-name ${!show ? 'd-none': 'show'}`}>Dashboard</span>
                </Link>
                <Link to='/bank' className='nav-link font-bold'>
                  <FontAwesomeIcon icon={faBank}/>
                  <span className={`nav-link-name ${!show ? 'd-none': 'show'}`}>Bank</span>
                </Link>
                <Link to='/admin' className='nav-link font-bold'>
                  <FontAwesomeIcon icon={faIntercom}/>
                  <span className={`nav-link-name ${!show ? 'd-none': 'show'}`}>Admin</span>
                </Link>
                <Link to='/summary' className='nav-link font-bold'>
                  <FontAwesomeIcon icon={faHistory}/>
                  <span className={`nav-link-name ${!show ? 'd-none': 'show'}`}>History</span>
                </Link>
              </div>
            </div>

            <div className="nav-link font-bold">
              <button onClick={logout} className='nav-link'>
                <FontAwesomeIcon icon={faSignOut}/>
                <span className={`nav-link-name ${!show ? 'd-none': 'show'}`}>Logout</span>
              </button>
            </div>
          </nav>
        </aside>

        <div className="card shadow border-none">
          <div className="card-body">
            
            <Outlet/>
          </div>
        </div>
      </main>
  );
};

export default Sidebar;