import { Link } from "react-router-dom"
import { Card } from 'primereact/card';

export default function CardBank() {

    return (
        <div className="conteiner">
            <div className="col-12 col-md-3 rounded">
                <Card className="md:w-20rem shadow">
                    <Link>
                        <div className="flex justify-content-between p-3 align-items-center">
                            <img src="/img/bca.png" alt="bca" style={{width: "50%"}}/>
                            <h1 className="text-center">Tulisan</h1>
                        </div>
                    </Link>
                </Card>
            </div>
        </div>
    )
}
        
        