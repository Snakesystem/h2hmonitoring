import { useRef, useState, useEffect } from 'react';
import useAuth from '../hooks/useAuth';
import { useNavigate, useLocation } from 'react-router-dom';
import Swal from 'sweetalert2'

// import axios from '../api/axios';
// import { toast } from 'react-toastify';
// const LOGIN_URL = '/user';

const Login = () => {
    const { setAuth } = useAuth();

    const navigate = useNavigate();
    const location = useLocation();
    const from = location.state?.from?.pathname || "/";

    const userRef = useRef();
    const errRef = useRef();

    const [user, setUser] = useState('');
    const [pwd, setPwd] = useState('');
    const [errMsg, setErrMsg] = useState('');

    // const [login, setLogin] = useState('')

    useEffect(() => {
        userRef.current.focus();
    }, [])

    useEffect(() => {
        setErrMsg('');
    }, [user, pwd])

    // useEffect(() => {
    //     axios.get(LOGIN_URL).then((res) => {
    //         setLogin(res.data)
    //     }).catch((error) => {
    //         toast.error(error.message)
    //     })
    // }, [])

    useEffect(() => {
        setUser(localStorage.getItem('user'))
        setPwd(localStorage.getItem('pwd'))
        if(user) {
            setAuth({user, pwd})
            navigate(from, { replace: true });
        }
    }, [])

    const handleSubmit = async (e) => {
        e.preventDefault();
 
        localStorage.setItem('user', user)
        localStorage.setItem('pwd', pwd)

        if(!user && !pwd) {
            Swal.fire({
                icon: "error",
                title: "Oops...",
                text: "Something went wrong!",
              });
        } else if(user && pwd) {
            setAuth({user, pwd});
            navigate(from, { replace: true });
            Swal.fire({
                title: "Login Succesfully!",
                text: "You clicked the button!",
                icon: "success"
              });

        } else{
            Swal.fire({
                icon: "error",
                title: "Oops...",
                text: "User no Found!", 
              });
        }

        // try {
        //     const response = await axios.post(LOGIN_URL,
        //         JSON.stringify({ user, pwd }),
        //         {
        //             headers: { 'Content-Type': 'application/json' },
        //             withCredentials: true
        //         }
        //     );
        //     console.log(JSON.stringify(response?.data));
        //     setAuth({ user, pwd});
        //     setUser('');
        //     setPwd('');
        //     navigate(from, { replace: true });

        // } catch (err) {
        //     if (!err?.response) {
        //         setErrMsg('No Server Response');
        //     } else if (err.response?.status === 400) {
        //         setErrMsg('Missing Username or Password');
        //     } else if (err.response?.status === 401) {
        //         setErrMsg('Unauthorized');
        //     } else {
        //         setErrMsg('Login Failed');
        //     }
        //     errRef.current.focus();
        // }
    }

    return (

        <section className="App">
            <div className="login card">
                <div className="card-body m-4">
                    <p ref={errRef} className={errMsg ? "errmsg" : "offscreen"} aria-live="assertive">{errMsg}</p>
                    <h1>Sign In</h1>
                    <form onSubmit={handleSubmit}>
                        <label htmlFor="username">Username:</label>
                        <input
                            type="text"
                            id="username"
                            ref={userRef}
                            autoComplete="off"
                            onChange={(e) => setUser(e.target.value)}
                            value={user}
                            required
                        />

                        <label htmlFor="password">Password:</label>
                        <input
                            type="password"
                            id="password"
                            onChange={(e) => setPwd(e.target.value)}
                            value={pwd}
                            required
                        />
                        <button className="btn btn-primary btn-rounded" type="submit">Sign In</button>
                    </form>
                </div>
            </div>
        </section>

    )
}

export default Login