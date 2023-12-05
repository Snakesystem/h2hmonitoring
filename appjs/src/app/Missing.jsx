import { Link } from "react-router-dom"

const Missing = () => {
    return (
        <div className="missing container align-items-center">
            <div className="row justify-content-center">
                <div className="col-8">
                    <img src="/img/NotFound.svg" alt="notfound" />
                </div>
            </div>
            <div className="row">
                <div className="col-12 text-center">
                    <h1>Oops!</h1>
                    <p className="text-danger">Page Not Found</p>
                    <div>
                        <Link className="text-decoration-none" to="/">Visit Our Homepage</Link>
                    </div>
                </div>
            </div>
        </div>
    )
}

export default Missing