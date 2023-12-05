import { useState } from 'react';
import Select from 'react-select';

const options = [
  { value: 'chocolate', label: 'Chocolate' },
  { value: 'strawberry', label: 'Strawberry' },
  { value: 'vanilla', label: 'Vanilla' },
];

export default function InputTypeHead() {
  
  const [selectedOption, setSelectedOption] = useState(null);
  console.log(selectedOption)

  const StyleOptions = {
    dropdownIndicator: () => ({
      display: "none"
    })
  }

  return (
    <div>
      <Select
        defaultValue={selectedOption}
        onChange={setSelectedOption}
        options={options}
        styles={StyleOptions}
      />
    </div>
  )
}
