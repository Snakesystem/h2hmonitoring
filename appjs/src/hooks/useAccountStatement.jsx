import { useQuery } from "react-query"
import { getAccountStatement } from "../service/api"

export default function useAccountStatement() {

    const { data, isError, isLoading, isFetching, isSuccess } = useQuery(
        "accountStatement",
        getAccountStatement,
        {
            staleTime: 3000,
            refetchInterval: 3000
        }
    )
    
  return (
    data, isError, isLoading, isFetching, isSuccess
  )
}
