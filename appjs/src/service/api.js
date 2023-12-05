import axios from 'axios';
import { toast } from 'react-toastify';

export const getAccountStatement = async (bank) => {
    try {
        const response = await axios.get(`api/${bank}/getdata/account-statement?Pass=WilliamDima270388`, {
            timeout: 10 * 1000,
        })

        if(response.status === 400 || response.status === 500) {
            throw response.data
        }

        return response.data;

    } catch (error) {
        toast.error('Something wen wrong', {
            autoClose: 3000,
            hideProgressBar: true,
            closeOnClick: true,
            pauseOnHover: true,
            draggable: true,
            progress: undefined,
            theme: "colored",
        })
        throw error
    }
}

export const getProducts = async () => {
    try {
        const response = await axios.get(`https://dummyjson.com/products`, {
            timeout: 10 * 1000,
        })

        if(response.status === 400 || response.status === 500) {
            throw response.data
        }

        return response.data;

    } catch (error) {
        toast.error('Something wen wrong', {
            autoClose: 3000,
            hideProgressBar: true,
            closeOnClick: true,
            pauseOnHover: true,
            draggable: true,
            progress: undefined,
            theme: "colored",
        })
        throw error
    }
}