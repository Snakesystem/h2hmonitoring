import axios from "axios";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom"

const Admin = () => {

    // const [products, setProducts] = useState([]);

    // useEffect(() => {
    //     axios.get('https://fakestoreapi.com/products').then((data) => {
    //         setProducts(data.data);
    //     });
    // }, []); 

    // console.log('produc', products)
    
    return (
        <section>
            <h1>Admins Page</h1>
            <br /> 
            <p>You must have been assigned an Admin role.</p> 
            <div className="flexGrow">
                <Link to="/">Home</Link>
            </div>
            {/* {
                products.map((product) => {
                    <ul key={product.toString()}>
                        <li>{product.title}{console.log(product)}</li>  
                    </ul>
                })
            } */}
        </section>
    )
}

export default Admin