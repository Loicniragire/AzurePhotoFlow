import {
    Chart as ChartJS,
    CategoryScale,
    LinearScale,
    BarElement,
    Title,
    Tooltip,
    Legend,
} from 'chart.js';
import { Bar } from 'react-chartjs-2';

ChartJS.register(
    CategoryScale,
    LinearScale,
    BarElement,
    Title,
    Tooltip,
    Legend
);

import { useEffect, useState } from 'react';
import axios from 'axios';
import '../styles/Dashboard.css';

const Dashboard = () => {
    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    useEffect(() => {
        const fetchDashboardData = async () => {
            try {
                const response = await axios.get('/api/dashboard');
                setData(response.data);
            } catch (err) {
                setError('Failed to load dashboard data. Please try again later.');
                console.error(err);
            } finally {
                setLoading(false);
            }
        };

        fetchDashboardData();
    }, []);

    if (loading) return <p>Loading dashboard data...</p>;
    if (error) return <p className="error-message">{error}</p>;

	const tagFrequencyData = {
		labels: data.tagFrequency?.map((item) => item.tag) || [], // Fallback to empty array
		datasets: [
			{
				label: 'Tag Frequency',
				data: data.tagFrequency?.map((item) => item.count) || [],
				backgroundColor: 'rgba(75, 192, 192, 0.2)',
				borderColor: 'rgba(75, 192, 192, 1)',
				borderWidth: 1,
			},
		],
	};

    const options = {
        scales: {
            y: {
                beginAtZero: true,
            },
        },
    };

    return (
        <div className="dashboard">
            <h2>Dashboard</h2>

            <div className="chart-container">
                <h3>Tag Frequency</h3>
                <Bar data={tagFrequencyData} options={options} />
            </div>

            <div className="stats-container">
                <h3>Summary Statistics</h3>
                <ul>
                    <li>Total Images Processed: {data.totalImages}</li>
                    <li>Average Processing Time: {data.avgProcessingTime} seconds</li>
                    <li>Model Accuracy: {data.modelAccuracy}%</li>
                </ul>
            </div>
        </div>
    );
};

export default Dashboard;

