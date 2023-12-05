/* eslint-disable no-unused-vars */
import { DataTable } from 'primereact/datatable';
import { Column } from 'primereact/column';
import { useEffect, useState } from "react";
import moment from "moment/moment";
import axios from "axios";
import { getAccountStatement } from "../service/api";
import { useQuery } from "react-query";
import { Button } from "primereact/button";
import { Dialog } from "primereact/dialog";
import { InputText } from "primereact/inputtext";
import { InputNumber } from "primereact/inputnumber";
import { Dropdown } from "primereact/dropdown";
import { Message } from 'primereact/message';
import { FilterMatchMode, FilterOperator } from 'primereact/api';
import { faExternalLink, faFileExcel, faFilePdf, faRefresh, faSearch } from '@fortawesome/free-solid-svg-icons';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';

const Bank = () => {

    const bank = [
        {
            BankID: 'bca', BankCode: 'bbca', BankName: "Bank Central Asia"
        },
        {
            BankID: 'bri', BankCode: 'bbca', BankName: "Bank Rakyat Indonesia"
        },
        {
            BankID: 'permata', BankCode: 'bbca', BankName: "Bank Permata"
        },
        {
            BankID: 'bni', BankCode: 'bbca', BankName: "Bank Negara Indonesia"
        },
    ]
    const [columns, setColumns] = useState([])

    useEffect(() => {
        axios.get(`api/${valueBank}/getdata/account-statement?Pass=WilliamDima270388`).then((data) => {
            setColumns(Object.keys(data.data[0]))
        })
        
    }, [])

    const [valueBank, setValueBank] = useState('bca')
    const [filters, setFilters] = useState(null);
    const [globalFilterValue, setGlobalFilterValue] = useState('');

    const [value, setValue] = useState('');
    const [selectedValue, setSelectedValue] = useState(null);
    const cities = [
        { name: 'New York', code: 'NY' },
        { name: 'Rome', code: 'RM' },
        { name: 'London', code: 'LDN' },
        { name: 'Istanbul', code: 'IST' },
        { name: 'Paris', code: 'PRS' },
        { name: 'Paris', code: 'PRS' },
        { name: 'Paris', code: 'PRS' },
        { name: 'Paris', code: 'PRS' },
        { name: 'Paris', code: 'PRS' },
        { name: 'Paris', code: 'PRS' },
        { name: 'Paris', code: 'PRS' },
        { name: 'Paris', code: 'PRS' },
        { name: 'Paris', code: 'PRS' },
        { name: 'Paris', code: 'PRS' },  
        { name: 'Paris', code: 'PRS' }, 
        { name: 'Paris', code: 'PRS' }
    ];

    const [selectData, setSelectData] = useState(null);
    const { data, isError, isLoading, isFetching, isSuccess } = useQuery(
        ["accountStatement", valueBank],
        () => getAccountStatement(valueBank), 
        // {
        //     staleTime: 3000,
        //     refetchInterval: 3000
        // }
    )

    // tableformater
    const tableFormatter = (data, value) => {

        if(typeof value === "object") {
            let valueFormatter = value.field ? value.field.toLowerCase() : "";
            console.log('value', value) 
            console.log('data', data)
            console.log('valueFormatter', valueFormatter)

            switch(valueFormatter) {
                case "transactiondate":
                case "receivetime":
                    value.field = dateFormatter(value); 
                    console.log('kkkk', value)
                    break; 
                case "amount":
                    value.field = currencyFormatter(value)
                    break;
            }
        }

    };

    const dateFormatter = (value) => {
        if(value)
            return moment(value).format('DD-MM-Y hh:mm:ss a');
            console.log('looo', value)
        return "-"
    }

    const currencyFormatter = (value) => {
        if(value)
            return new Intl.NumberFormat('id-ID', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value.FaceValue);
            console.log('ahahahah', value)
        return "0.00" 

    }

    // export to excel
    const exportExcel = () => {
        import('xlsx').then((xlsx) => {
            const worksheet = xlsx.utils.json_to_sheet(data);
            const workbook = { Sheets: { data: worksheet }, SheetNames: ['data'] };
            const excelBuffer = xlsx.write(workbook, {
                bookType: 'xlsx',
                type: 'array'
            });

            import('file-saver').then((module) => {
                if (module && module.default) {
                    let EXCEL_TYPE = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;charset=UTF-8';
                    let EXCEL_EXTENSION = '.xlsx';
                    const data = new Blob([excelBuffer], {
                        type: EXCEL_TYPE
                    });
    
                    module.default.saveAs(data, 'AccountStatement' + '_export_' + new Date().getTime() + EXCEL_EXTENSION);
                }
            });
        });
    };

    // export to pdf
    const exportColumns = columns.map((col) => ({ title: col, dataKey: col }));

    const exportPdf = () => {
        import('jspdf').then((jsPDF) => {
            import('jspdf-autotable').then(() => {
                const doc = new jsPDF.default(0, 0);

                doc.autoTable(exportColumns, data);
                doc.save('products.pdf');
            });
        });
    };

    const MessageError = () => {
        return <Message className="p-2 w-100" severity="error" text="Data not found" />
    }

    const [visible, setVisible] = useState(false);
    const [position, setPosition] = useState('center');
    const footerContent = (
        <div className="p-3">
            <div className="flex gap-2 justify-content-end">
                <Button className="rounded px-3" severity="danger" icon={<FontAwesomeIcon icon={faRefresh}/>} onClick={() => setValue(null)} />
                <Button className="rounded px-3" severity="info" icon={<FontAwesomeIcon icon={faSearch}/>} onClick={() => setVisible(false)} autoFocus />
            </div>
        </div>
    );

    const show = (position) => {
        setPosition(position);
        setVisible(true);
    };

    // filter control
    const clearFilter = () => {
        initFilters();
    };

    const onGlobalFilterChange = (e) => {
        const value = e.target.value;
        let _filters = { ...filters };

        _filters['global'].value = value;

        setFilters(_filters);
        setGlobalFilterValue(value);
    };

    const initFilters = () => {
        setFilters({
            global: { value: null, matchMode: FilterMatchMode.CONTAINS },
            'columns.Statement_ID': { operator: FilterOperator.AND, constraints: [{ value: null, matchMode: FilterMatchMode.STARTS_WITH }] },
            "columns.ClientID": { operator: FilterOperator.AND, constraints: [{ value: null, matchMode: FilterMatchMode.STARTS_WITH }] },
            "columns.Amount": { value: null, matchMode: FilterMatchMode.IN },
            "columns.FundInOutNID": { value: null, matchMode: FilterMatchMode.IN },
            "columns.FundNID": { value: null, matchMode: FilterMatchMode.IN },
            "columns.TransactionDate": { operator: FilterOperator.AND, constraints: [{ value: null, matchMode: FilterMatchMode.DATE_IS }] },
            "columns.TransactionType": { operator: FilterOperator.AND, constraints: [{ value: null, matchMode: FilterMatchMode.EQUALS }] },
            "columns.StatusBO": { operator: FilterOperator.OR, constraints: [{ value: null, matchMode: FilterMatchMode.EQUALS }] },
            "columns.Reason": { value: null, matchMode: FilterMatchMode.BETWEEN },
            "columns.ExternalReference": { value: null, matchMode: FilterMatchMode.EQUALS },
            "columns.ReceiveTime": { operator: FilterOperator.AND, constraints: [{ value: null, matchMode: FilterMatchMode.DATE_IS }] }
        });
        setGlobalFilterValue('');
    };

    const renderHeader = () => {
        return (
            <div className="container my-3">
                <div className="row">
                    <div className="col-lg-4">
                        <h3>Account Statement</h3>
                    </div>
                    <div className="col-lg-8">
                        <div className="flex justify-content-end gap-2">
                            <InputText value={globalFilterValue} onChange={onGlobalFilterChange} placeholder="Select value to Search..." style={{padding: 10}} />
                            <Button className="rounded-circle" type="button" icon={<FontAwesomeIcon icon={faRefresh}/>} rounded text raised severity="success" onClick={clearFilter} />
                            <Button className="rounded-circle" icon={<FontAwesomeIcon icon={faSearch}/>} rounded text raised severity="success" aria-label="Search" onClick={() => show(setSelectedValue(null))}/>
                            <Button className="rounded-circle" icon={<FontAwesomeIcon icon={faExternalLink}/>} rounded text raised severity="success" aria-label="Show" />
                            <Button className="rounded-circle" icon={<FontAwesomeIcon icon={faFileExcel}/>} rounded text raised severity="success" aria-label="Export to Excel" onClick={exportExcel} data-pr-tooltip="XLS" />
                            <Button className="rounded-circle" icon={<FontAwesomeIcon icon={faFilePdf}/>} rounded text raised severity="success" aria-label="Export to PDF" onClick={exportPdf}/>
                        </div>
                    </div>
                </div>
            </div>
        );
    };

    const fetchData = () => {
        return(
            <div className="container">
                <div className="row">
                    <div className="col-12">
                        {isError && <h3>Ada error</h3>}
                        {isLoading && <h3>Sebentar......</h3>}
                    </div>
                </div>
            </div>
        )
    }

    const loader = fetchData()
    const header = renderHeader()
    const errorMsg = MessageError()

    return (
        <section>
            <h1>Bank Table</h1>
            <hr />
            <div className="col-12">
                <div className="conteiner">
                    <div className="row justify-content-center">
                        <div className="col-12 rounded">
                            <div className="row px-2 gap-2 justify-content-between" >
                                {
                                    bank.map(
                                        (dataBank) => 
                                        <Button 
                                            text raised type='button' 
                                            className="text-decoration-none text-dark rounded shadow col-lg-6" 
                                            style={{maxWidth: "22rem"}}
                                            key={dataBank.BankID} 
                                            onClick={() => setValueBank(dataBank.BankID)}>
                                            <div className="row justify-content-between px-1 align-items-center"> 
                                                <div className="flex justify-content-between" style={{margin: "3px 0 -15px 0"}}>
                                                    <h6 className='font-bold'>Balance</h6>
                                                    <h6 className='font-bold'>Rp 37000</h6>
                                                    <h6 className='font-bold'>10%</h6>
                                                </div>
                                                <div className="col-4">
                                                    <img src={`/img/${dataBank.BankID}.png`} alt={dataBank.BankName} style={{width: "100%"}}/>
                                                </div>
                                                <div className="col-8">
                                                    <div className="row">
                                                        <div className="col-4">
                                                            Success <br />1000
                                                        </div>
                                                        <div className="col-4">
                                                            Skiped <br />1000
                                                        </div>
                                                        <div className="col-4">
                                                            Resend <br />1000
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>
                                        </Button>
                                    )
                                }
                            </div>
                        </div>
                    </div>
                </div>
                <hr />
                <div className="row justify-content-between">
                    <DataTable 
                        value={data} 
                        scrollable scrollHeight="400px" 
                        className="col-12 p-2 shadow rounded" 
                        header={header}
                        paginator 
                        selection={selectData}
                        selectionMode="single"
                        onSelectionChange={(e) => setSelectData(e.value)}
                        emptyMessage={errorMsg}
                        filters={filters} 
                        globalFilterFields={columns}
                        loading={!isSuccess && loader}
                        rows={20}>
                        { 
                            columns.map((item) => 
                                <Column 
                                    key={item} 
                                    field={item} 
                                    header={item} 
                                    body={data? tableFormatter(): 'kaga ada'}
                                    style={{padding: 5, border: "double rgba(138,125,255,0.4)", fontSize: 14, whiteSpace: "nowrap"}}>
                                </Column>
                            )
                        }

                    </DataTable>
                    <Dialog 
                        header={<h3 className="mt-4">Filter Control</h3>} 
                        visible={visible} 
                        position={position} 
                        className="text-center"
                        style={{ width: '20rem' }}
                        onHide={() => setVisible(false)} 
                        footer={footerContent} 
                        draggable={false} 
                        resizable={false}>

                        <div className="col-12 px-3">
                            <div className="row">
                                <div className="flex flex-column">
                                    <label htmlFor="">Number</label>
                                    <InputNumber id="" value={value} onValueChange={(e) => setValue(e.value)} minFractionDigits={2} />
                                </div>
                                <div className="flex flex-column">
                                    <label htmlFor="">Percent</label>
                                    <InputNumber id="" value={value} onValueChange={(e) => setValue(e.value)} prefix="%" />
                                </div>
                                <div className="flex flex-column">
                                    <label htmlFor="">Text</label>
                                    <InputText className="p-2" value={value} onChange={(e) => setValue(e.target.value)} style={{height: 40}} />
                                </div>
                                <div className="flex flex-column">
                                    <label htmlFor="">Dropdown</label>
                                    <Dropdown value={selectedValue} onChange={(e) => setSelectedValue(e.value)} options={cities} optionLabel="name" placeholder="Choise an option" className="w-full" style={{height: 40, alignItems: "center"}} />
                                </div>
                                <div className="flex flex-column">
                                    <label htmlFor="">Date</label>
                                    <input type="date" className="forn-control"/>
                                </div>
                            </div>
                        </div>
                    </Dialog>
                </div>
            </div>
        </section>
    )
}

export default Bank