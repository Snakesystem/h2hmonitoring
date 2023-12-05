/* eslint-disable react/prop-types */
/* eslint-disable no-unused-vars */
import { useState, useEffect, useRef } from 'react';
import { DataTable } from 'primereact/datatable';
import { Column } from 'primereact/column';
import axios from 'axios';
import { Toast } from 'primereact/toast';

export default function PrimeReactTable() {
    const [data, setData] = useState([]);
    const [header, setHeader] = useState([])

    const toast = useRef(null);

    const [loading, setLoading] = useState(true);

    useEffect(() => {
        axios.get('http://localhost:3000/bondmatured').then((data) => {
            setHeader(data.data);
            setLoading(false);
        });
    }, []); 

    useEffect(() => {
        axios.get('api/bca/getdata/account-statement?Pass=WilliamDima270388').then((data) => {
            setData(data.data);
            setLoading(false);
        });
    }, []); 

    return (
        <div className="border">
            <DataTable 
                className='border p-3' 
                value={data} 
                scrollable 
                scrollHeight="40rem" 
                paginator rows={5} 
                rowsPerPageOptions={[5, 10, 25, 50]} 
                tableStyle={{ minWidth: '100%' }}
                loading={loading}  
                emptyMessage="Data not found."   
                > 
                {header.map((item => <Column key={item} field={item.field} header={item.title}/>))}
            </DataTable> 
        </div>
    );
}